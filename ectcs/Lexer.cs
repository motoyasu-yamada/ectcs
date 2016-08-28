using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ectcs
{
  /*
   * Template Lexer
   */
  public sealed class EctLexer
  {
    private enum Mode
    {
      Html, Script
    }
    private static readonly Regex NewlineRegex = new Regex("\n", RegexOptions.Multiline);
    private static readonly Dictionary<string, EctToken> Keywords = new Dictionary<string, EctToken>
    {
      { "include" , EctToken.Keyword_Include },
      { "content" , EctToken.Keyword_Content },
      { "extend" , EctToken.Keyword_Extend },
      { "block" , EctToken.Keyword_Block },
      { "if" , EctToken.Keyword_If },
      { "else" , EctToken.Keyword_Else },
      { "for" , EctToken.Keyword_For },
      { "in" , EctToken.Keyword_In },
      { "switch" , EctToken.Keyword_Switch },
      { "when" , EctToken.Keyword_When },
      { "end" , EctToken.Keyword_End },
    };
    private static readonly Dictionary<string, EctToken> Operators = new Dictionary<string, EctToken>
    {
      {"@",EctToken.Atmark }, { ".", EctToken.Dot}, { "?.", EctToken.QuestionDot},{ ",", EctToken.Comma },
      {"{",EctToken.ObjectStart }, { "}", EctToken.ObjectEnd},
      {"[",EctToken.ArrayStart }, { "]", EctToken.ArrayEnd},
      {"(",EctToken.ParenStart }, { ")", EctToken.ParenEnd},
      { "+", EctToken.Add}, { "-", EctToken.Sub}, { "*", EctToken.Mul}, { "/", EctToken.Div},{ "%", EctToken.Mod},
      { "+=", EctToken.LetAdd}, { "-=", EctToken.LetSub}, { "*=", EctToken.LetMul}, { "/=", EctToken.LetDiv},{ "%=", EctToken.LetMod},
      {"++",EctToken.Inc }, { "--", EctToken.Dec},
      {"=",EctToken.Let }, { "==", EctToken.Eq},{"===",EctToken.Eq }, { "!=", EctToken.Ne},{ "!==", EctToken.Ne},
      { "<", EctToken.Lt},{ "<=", EctToken.Le}, { ">", EctToken.Gt},{ ">=", EctToken.Ge},
      { "?", EctToken.Question},{ ":", EctToken.Collon}, { "??", EctToken.DoubleQuestion},
      { "!", EctToken.Not },{ "&&", EctToken.And },{ "||", EctToken.Or },
      { "->", EctToken.Lambda }, { "=>", EctToken.Lambda }
    };
    private static readonly int[] SymboCapablelLength = new int[256];
    private static readonly bool[] Symbols = new bool[256];
    static EctLexer()
    {
      foreach (var s in Operators.Keys)
      {
        var n = s[0];
        SymboCapablelLength[n] = Math.Max(SymboCapablelLength[n], s.Length);
        foreach (var c in s)
        {
          Symbols[c] = true;
        }
      }
    }

    public string TemplateName { get; private set; }
    public int LineNumber { get; private set; }
    public int ColNumber { get; private set; }
    public EctToken CurrentToken { get; private set; }
    public string CurrentValue { get; private set; }

    private string openTag;
    private string closeTag;
    private Mode mode = Mode.Html;
    private string template;
    private int current;
    private int end;
    private int scriptStart;
    private int scriptEnd;

    public EctLexer(string templateName, string template, int start, int length, EctOptions options)
    {
      if (templateName == null)
      {
        throw new ArgumentNullException("templateName");
      }
      if (template == null)
      {
        throw new ArgumentNullException("template");
      }
      if (start < 0 || template.Length <= start)
      {
        throw new ArgumentOutOfRangeException("start");
      }
      if (length <= 0 || template.Length < (length + start))
      {
        throw new ArgumentOutOfRangeException("length");
      }
      if (options == null)
      {
        throw new ArgumentNullException("options");
      }
      this.TemplateName = templateName;
      this.template = template;
      this.current = start;
      this.end = start + length;
      this.LineNumber = 1;
      this.ColNumber = 1;
      this.openTag = options.Open;
      this.closeTag = options.Close;
    }

    private void CountLine(string text)
    {
      int start = 0;
      var end = start + text.Length;
      while (start < end)
      {
        var m = NewlineRegex.Match(text, start);
        if (!m.Success)
        {
          break;
        }
        LineNumber++;
        start = m.Index + 1;
      }
      ColNumber = end - start;
    }

    private bool prefetched;
    private EctToken prefetchedToken;
    private string prefetchedValue;
    private bool hasPrevious;
    private EctToken previousToken;
    private string previousValue;

    public EctToken NextToken()
    {
      if (prefetched)
      {
        prefetched = false;
        CurrentToken = prefetchedToken;
        CurrentValue = prefetchedValue;
        return CurrentToken;
      }
      previousToken = CurrentToken;
      previousValue = CurrentValue;
      hasPrevious = true;
      return Next();
    }

    private void BackToken()
    {
      if (prefetched)
      {
        throw new InvalidProgramException();
      }
      if (!hasPrevious)
      {
        throw new InvalidProgramException();
      }
      prefetched = true;
      hasPrevious = false;
      prefetchedToken = CurrentToken;
      prefetchedValue = CurrentValue;
      CurrentToken = previousToken;
      CurrentValue = previousValue;
    }

    private EctToken Next()
    {
restart:
      if (end <= current)
      {
        CurrentToken = EctToken.Eof;
        CurrentValue = null;
      }
      else if (mode == Mode.Html)
      {
        var i = template.IndexOf(openTag, current, end - current);
        if (i == -1)
        {
          CurrentValue = template.Substring(current, end - current);
          current = end;
        }
        else
        {
          CurrentValue = template.Substring(current, i - current);
          current = i + openTag.Length;
          scriptStart = current;
        }
        CurrentToken = EctToken.Html;
        if (current < end)
        {
          mode = Mode.Script;
          scriptEnd = template.IndexOf(closeTag, current, end - current);
        }
      }
      else // mode == Mode.Scirpt
      {
        if (scriptEnd == -1)
        {
          CurrentToken = EctToken.Error_ScriptNotClosed;
          CurrentValue = null;
          goto finish;
        }
        var c = NextChar();
        if (scriptStart == current - 1)
        {
          if (c == '=')
          {
            current++;
            CurrentToken = EctToken.Escape;
            CurrentValue = null;
            goto finish;
          }
          if (c == '-')
          {
            current++;
            CurrentToken = EctToken.Unescape;
            CurrentValue = null;
            goto finish;
          }
        }
loop:
        if (scriptEnd == current)
        {
          mode = Mode.Html;
          current += closeTag.Length;
          ColNumber += closeTag.Length;
          scriptEnd = -1;
          goto restart;
        }
        if (Char.IsWhiteSpace(c))
        {
          c = NextChar();
          goto loop;
        }

        if (c == '\'' || c == '"')
        {
          ParseString(c);
          goto finish;
        }
        if (Char.IsDigit(c))
        {
          ParseNumber();
          goto finish;
        }
        if (Char.IsLetter(c))
        {
          ParseIdentOrKeyword();
          goto finish;
        }
        if (c < Symbols.Length && Symbols[c])
        {
          ParseOperator(c);
          goto finish;
        }

        CurrentToken = EctToken.Error_Invalid;
        CurrentValue = c.ToString();
        goto finish;
      }
finish:
      Debug.WriteLine("{0},{1}", CurrentToken, CurrentValue);
      return CurrentToken;
    }

    private void ParseOperator(char c)
    {
      int canBe = SymboCapablelLength[c] - 1;
      string sym = c.ToString();
      for (var i = 0; i < canBe; i++)
      {
        var p = current + i;
        var c2 = p < end ? template[p] : '\0';
        if (c2 < Symbols.Length && Symbols[c2])
        {
          sym += c2;
        }
        else
        {
          break;
        }
      }
      for (;;)
      {
        EctToken t;
        if (Operators.TryGetValue(sym, out t))
        {
          CurrentToken = t;
          CurrentValue = null;
          current += sym.Length - 1;
          ColNumber += sym.Length - 1;
          return;
        }
        if (sym.Length == 1)
        {
          break;
        }
        sym = sym.Substring(0, sym.Length - 1);
      }
      CurrentToken = EctToken.Error_Invalid;
      CurrentValue = sym;
    }

    private void ParseIdentOrKeyword()
    {
      var start = current - 1;
      var pre = 0;
      for (; ; pre++)
      {
        var cp = Prefetch(0);
        if (char.IsLetterOrDigit(cp))
        {
          NextChar();
          continue;
        }
        break;
      }
      CurrentValue = template.Substring(start, pre + 1);
      EctToken t;
      if (Keywords.TryGetValue(CurrentValue, out t))
      {
        CurrentToken = t;
      }
      else
      {
        CurrentToken = EctToken.Ident;
      }
    }

    private void ParseNumber()
    {
      CurrentToken = EctToken.Literal_Number;

      var start = current - 1;
      var dotted = false;
      var pre = 0;
      for (; ; pre++)
      {
        var cp = Prefetch(0);
        if (cp == 0)
        {
          break;
        }
        if (char.IsWhiteSpace(cp))
        {
          break;
        }
        if (char.IsDigit(cp))
        {
          NextChar();
          continue;
        }
        if (!dotted && char.IsDigit(cp))
        {
          NextChar();
          dotted = true;
          continue;
        }
        CurrentToken = EctToken.Error_Invalid;
      }
      CurrentValue = template.Substring(start, pre);
    }

    private void ParseString(char separator)
    {
      var sb = new StringBuilder();
      bool escaping = false;
      while (current < end)
      {
        var c = NextChar();
        if (escaping)
        {
          if (c == separator)
          {
            sb.Append(c);
          }
          else
          {
            switch (c)
            {
              case 'n':
                sb.Append('\n');
                break;
              case 'r':
                sb.Append('\n');
                break;
              case '0':
                sb.Append('\n');
                break;
              case 't':
                sb.Append('\n');
                break;
              case '\\':
                sb.Append('\n');
                break;
              default:
                break;
            }
          }
          escaping = false;
        }
        else
        {
          if (c == separator)
          {
            CurrentToken = EctToken.Literal_String;
            CurrentValue = sb.ToString();
            return;
          }
          sb.Append(c);
        }
      }
      CurrentToken = EctToken.Error_Invalid;
      CurrentValue = sb.ToString();
    }

    private char Prefetch(int pre, char eof = '\0')
    {
      var p = current + pre;
      return p < end ? template[p] : eof;
    }

    private char NextChar()
    {
      var c = template[current++];
      if (c == '\n')
      {
        LineNumber++;
        ColNumber = 1;
      }
      else
      {
        ColNumber++;
      }
      return c;
    }
  }
}

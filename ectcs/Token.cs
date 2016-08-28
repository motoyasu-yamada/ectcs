using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ectcs
{
  /**
   * Template Tokens
   */
  public enum EctToken
  {
    Eof,
    Error_ScriptNotClosed, Error_Invalid,
    Html,
    Escape, Unescape,
    Keyword_Include, Keyword_Content, Keyword_Extend,
    Keyword_Block, Keyword_If, Keyword_Else, Keyword_For, Keyword_In, Keyword_Switch, Keyword_When, Keyword_End,
    Ident,
    Const_Null, Const_True, Const_False,
    Literal_String, Literal_Number,

    Atmark, Dot, QuestionDot, Comma,
    ObjectStart, ObjectEnd,
    ArrayStart, ArrayEnd,
    ParenStart, ParenEnd,
    Add, Sub, Mul, Div, Mod,
    LetAdd, LetSub, LetMul, LetDiv, LetMod,
    Inc, Dec,
    Let, Eq, Ne, Gt, Lt, Ge, Le,
    Question, DoubleQuestion, Collon,
    Not, And, Or,
    Lambda
  }
}

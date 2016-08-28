using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ectcs
{
  public class EctCompiler
  {
    private EctLexer lexer;
    private List<EctError> errors = new List<EctError>();
    private Dictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();
    private ParameterExpression self = Expression.Parameter(typeof(object), "self");
    private ParameterExpression context = Expression.Parameter(typeof(EctRuntimeContext), "context");

    public EctCompiler(EctLexer lexer)
    {
      if (lexer == null)
      {
        throw new ArgumentNullException("lexer");
      }
      this.lexer = lexer;
    }

    private ParameterExpression Variable(string ident)
    {
      ParameterExpression pe;
      if (!variables.TryGetValue(ident, out pe))
      {
        pe = Expression.Parameter(typeof(object), ident);
        variables[ident] = pe;
      }
      return pe;
    }

    public Action<EctRuntimeContext, object> Compile()
    {
      lexer.NextToken();

      var statements = new List<Expression>();
      ParseStatements(statements);
      CheckCurrentToken(EctToken.Eof);

      if (errors.Count != 0)
      {
        foreach (var e in errors)
        {
          Debug.WriteLine("{0} '{1}'({2},{3})", e.Message, e.TemplateName, e.LinePos, e.ColumnPos);
        }
        throw new InvalidProgramException();
      }
      else
      {
        var body = Expression.Block(variables.Values, statements);
        var lambdaExpression = Expression.Lambda(body, context, self);
        var lambda = lambdaExpression.Compile();
        return lambda as Action<EctRuntimeContext, dynamic>;
      }
    }

    private Expression ParseLayout()
    {
      lexer.NextToken();
      if (!CheckCurrentToken(EctToken.Literal_String))
      {
        return null;
      }
      var layoutName = lexer.CurrentValue;
      lexer.NextToken();

      return Expression.Call(context, EctRuntimeContext.IncludeMethod, Expression.Constant(layoutName), Expression.Constant(null));
    }

    private Expression ParseBlock()
    {
      string blockName;
      lexer.NextToken();
      if (lexer.CurrentToken == EctToken.Literal_String)
      {
        blockName = lexer.CurrentValue;
        lexer.NextToken();
      }
      else
      {
        blockName = "content";
      }
      ParseIndent();

      var block = new List<Expression>();
      ParseStatements(block);
      if (!CheckCurrentToken(EctToken.Keyword_End))
      {
        return null;
      }
      lexer.NextToken();

      var body = Expression.Block(block);
      var parameters = new[] { context, self };
      var blockLambda = Expression.Lambda<Action<EctRuntimeContext, object>>(body, parameters);
      return Expression.Call(context, EctRuntimeContext.DefineBlockMethod, Expression.Constant(blockName), blockLambda);
    }

    private void ParseStatements(List<Expression> statements)
    {
      for (;;)
      {
        Expression e = null;
        var t = lexer.CurrentToken;
        switch (t)
        {
          case EctToken.Eof:
            return;
          case EctToken.Html:
            e = ParseHtml();
            break;
          case EctToken.Keyword_Extend:
            e = ParseLayout();
            break;
          case EctToken.Keyword_Block:
            e = ParseBlock();
            break;
          case EctToken.Keyword_Content:
            e = ParseContent();
            break;
          case EctToken.Escape:
            e = ParseEscape();
            break;
          case EctToken.Unescape:
            e = ParseUnescape();
            break;
          case EctToken.Keyword_Include:
            e = ParseInclude();
            break;
          case EctToken.Keyword_For:
            e = ParseFor();
            break;
          case EctToken.Keyword_Switch:
            e = ParseSwitch();
            break;
          case EctToken.Keyword_If:
            e = ParseIf();
            break;
          case EctToken.Keyword_Else:
          case EctToken.Keyword_End:
          case EctToken.Keyword_In:
          case EctToken.Keyword_When:
            return;
          default:
            e = ParseAssignmentExpression();
            if (e == null)
            {
              return;
            }
            break;
        }
        if (e != null)
        {
          statements.Add(e);
        }
      }
    }

    private Expression ParseContent()
    {
      lexer.NextToken();
      string contentBlockName;
      if (lexer.CurrentToken == EctToken.Literal_String)
      {
        contentBlockName = lexer.CurrentValue;
        lexer.NextToken();
      }
      else
      {
        contentBlockName = "content";
      }
      var blockLambda = Expression.Call(context, EctRuntimeContext.CallBlockMethod, Expression.Constant(contentBlockName));
      return Expression.Invoke(blockLambda, context, self);
    }


    private Expression Test(Expression test)
    {
      return Expression.Call(null, EctRuntime.TestMethod, test);
    }

    private Expression ParseSwitch()
    {
      lexer.NextToken();

      var switchValue = ParseAssignmentExpression();
      if (switchValue == null)
      {
        SyntaxError("switch requires test expression");
        return null;
      }
      ParseIndent();

      Expression defaultBody = null;
      var cases = new List<SwitchCase>();
      var finish = false;
      while (!finish)
      {
        List<Expression> statements;
        switch (lexer.CurrentToken)
        {
          case EctToken.Keyword_When:
            lexer.NextToken();

            Expression caseTestValue = ParseAssignmentExpression();
            ParseIndent();

            statements = new List<Expression>();
            ParseStatements(statements);
            if (!CheckCurrentToken(EctToken.Keyword_End))
            {
              return null;
            }
            cases.Add(Expression.SwitchCase(Expression.Block(statements), caseTestValue));
            break;
          case EctToken.Keyword_Else:
            lexer.NextToken();

            if (defaultBody != null)
            {
              SyntaxError("Duplicated else");
              return null;
            }

            ParseIndent();

            statements = new List<Expression>();
            ParseStatements(statements);
            if (!CheckCurrentToken(EctToken.Keyword_End))
            {
              return null;
            }
            defaultBody = Expression.Block(statements);
            break;
          case EctToken.Keyword_End:
            finish = true;
            lexer.NextToken();
            ParseIndent();
            break;
          default:
            SyntaxError("switch requires when, else or end.");
            return null;
        }
      }
      return Expression.Switch(switchValue, defaultBody, cases.ToArray());

    }

    private Expression ParseIf()
    {
      lexer.NextToken();

      var test = ParseAssignmentExpression();
      if (test == null)
      {
        SyntaxError("if requires test expression");
        return null;
      }
      ParseIndent();

      var ifTrue = new List<Expression>();
      ParseStatements(ifTrue);

      var ifFalse = new List<Expression>();
      if (lexer.CurrentToken == EctToken.Keyword_Else)
      {
        lexer.NextToken();
        ParseIndent();
        ParseStatements(ifFalse);
      }

      if (!CheckCurrentToken(EctToken.Keyword_End))
      {
        return null;
      }
      lexer.NextToken();

      return Expression.IfThenElse(
        Test(test),
        ifTrue.Count != 0 ? Expression.Block(ifTrue) as Expression : Expression.Empty(),
        ifFalse.Count != 0 ? Expression.Block(ifFalse) as Expression : Expression.Empty());
    }

    private Expression ParseFor()
    {
      lexer.NextToken();

      if (!CheckCurrentToken(EctToken.Ident))
      {
        return null;
      }
      var elementVarName = lexer.CurrentValue;
      lexer.NextToken();

      if (!CheckCurrentToken(EctToken.Keyword_In))
      {
        return null;
      }
      lexer.NextToken();

      var collection = ParseAssignmentExpression();
      ParseIndent();

      var loopContent = new List<Expression>();
      ParseStatements(loopContent);

      if (!CheckCurrentToken(EctToken.Keyword_End))
      {
        return null;
      }
      lexer.NextToken();

      var elementType = typeof(object);
      var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
      var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

      var enumerableExpression = Expression.Convert(collection, enumerableType);
      var enumeratorVar = Expression.Variable(enumeratorType);

      var getEnumeratorCall = Expression.Call(enumerableExpression, enumerableType.GetMethod("GetEnumerator"));
      var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);
      var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));
      var elementVar = Variable(elementVarName);
      var elementAssign = Expression.Assign(elementVar, Expression.Property(enumeratorVar, "Current"));
      var breakLabel = Expression.Label();
      var ifTest = Expression.Equal(moveNextCall, Expression.Constant(true));

      loopContent.Insert(0, elementAssign);
      var ifThen = Expression.Block(new[] { elementVar }, loopContent);
      var ifElse = Expression.Break(breakLabel);

      return Expression.Block(new[] { enumeratorVar }, enumeratorAssign, Expression.Loop(Expression.IfThenElse(ifTest, ifThen, ifElse), breakLabel));
    }

    private Expression ParseForInBinding()
    {
      return null;
    }

    private Expression ParseHtml()
    {
      var html = lexer.CurrentValue;
      lexer.NextToken();
      return Expression.Call(context, EctRuntimeContext.UnescapeMethod, new[] { Expression.Constant(html) });
    }

    private Expression ParseEscape()
    {
      return ParseOutput(EctRuntimeContext.EscapeMethod);
    }

    private Expression ParseUnescape()
    {
      return ParseOutput(EctRuntimeContext.UnescapeMethod);
    }

    private Expression ParseOutput(MethodInfo method)
    {
      lexer.NextToken();
      var e = ParseExpressionStatement();
      if (e != null)
      {
        return Expression.Call(context, method, new[] { e });
      }
      return null;
    }

    private Expression ParseExpressionStatement()
    {
      return ParseAssignmentExpression();
    }

    private Expression MakeParenExpression(List<Expression> expressions)
    {
      if (expressions.Count == 0)
      {
        SyntaxError("Empty paren expression");
        return null;
      }
      return Expression.Block(expressions);

    }

    private Expression ParseParenOrLambdaExpression()
    {
      if (!CheckCurrentToken(EctToken.ParenStart))
      {
        return null;
      }
      lexer.NextToken();

      var expressions = new List<Expression>();
      if (lexer.CurrentToken == EctToken.ParenEnd)
      {
        lexer.NextToken();
      }
      else
      {
        for(var c = true; c;)
        {
          var e = ParseAssignmentExpression();
          if (e == null)
          {
            return null;
          }
          if (!CheckCurrentToken(EctToken.Comma, EctToken.ParenEnd))
          {
            return null;
          }
          expressions.Add(e);
          c = lexer.CurrentToken == EctToken.Comma;
          lexer.NextToken();
        }
      }

      if (lexer.CurrentToken != EctToken.Lambda)
      {
        return MakeParenExpression(expressions);
      }
      lexer.NextToken();

      var mustBeParen = !expressions.All(e => e is ParameterExpression);
      if (mustBeParen)
      {
        return MakeParenExpression(expressions);
      }

      var startToken = lexer.CurrentToken;
      var endToken = startToken == EctToken.ObjectStart ? EctToken.ObjectEnd : EctToken.Keyword_End;
      var statements = new List<Expression>();
      ParseStatements(statements);
      if (!CheckCurrentToken(endToken))
      {
        return null;
      }
      lexer.NextToken();

      var lambdaBody = Expression.Block(statements);
      var parameters = expressions.Select(e => e as ParameterExpression);

      return Expression.Lambda(Expression.Block(lambdaBody), parameters);
    }

    private Expression ParsePrimaryExpression()
    {
      Expression e = null;
      var t = lexer.CurrentToken;
      switch (t)
      {
        case EctToken.Const_Null:
          e = Expression.Constant(null);
          break;
        case EctToken.Const_True:
          e = Expression.Constant(true);
          break;
        case EctToken.Const_False:
          e = Expression.Constant(false);
          break;
        case EctToken.Literal_String:
          e = Expression.Constant(lexer.CurrentValue as dynamic);
          lexer.NextToken();
          break;
        case EctToken.Literal_Number:
          e = ParseNumber();
          break;
        case EctToken.ParenStart:
          e = ParseParenOrLambdaExpression();
          break;
        //case EctToken.ArrayStart:
        //  ParseArrayInitialiser();
        //  break;
        //case EctToken.ObjectStart:
        //  ParseObjectInitialiser();
        //  break;
        //case null:
        //  ParseFunctionExpression();
        //  break;
        case EctToken.Atmark:
          e = ParseThisExpression();
          break;
        //case EctToken.Ident:
        //  ParseLetExpression();         
        //  break;
        case EctToken.Ident:
          var ident = lexer.CurrentValue;
          e = Variable(ident);
          lexer.NextToken();
          break;
        default:
          break;
      }
      return e;
    }

    private Expression ParseNumber()
    {
      if (lexer.CurrentToken != EctToken.Literal_Number)
      {
        throw new InvalidProgramException();
      }
      var v = lexer.CurrentValue;
      lexer.NextToken();
      int i;
      if (int.TryParse(v, out i))
      {
        Expression.Constant(i as dynamic);
      }
      double d;
      if (double.TryParse(v, out d))
      {
        Expression.Constant(d as dynamic);
      }
      throw new InvalidProgramException();
    }

    private Expression ParseMemberExpression()
    {
      Expression e = ParsePrimaryExpression();
      if (e == null)
      {
        return null;
      }
      for (;;)
      {
        var e2 = ParsePropertyOperator(e);
        if (e2 == null)
        {
          return e;
        }
        e = e2;
      }
    }

    private Expression[] ParseArguments()
    {
      if (lexer.CurrentToken != EctToken.ParenStart)
      {
        return null;
      }
      lexer.NextToken();

      var args = new List<Expression>();
      for (;;)
      {
        var a = ParseAssignmentExpression();
        if (a == null)
        {
          break;
        }
        args.Add(a);
        if (lexer.CurrentToken != EctToken.Comma)
        {
          break;
        }
        lexer.NextToken();
      }
      if (!CheckCurrentToken(EctToken.ParenEnd))
      {
        return null;
      }
      lexer.NextToken();

      return args.ToArray();
    }

    private Expression MakeInvokeExpression(Expression lambda, Expression[] arguments)
    {
      var argumentArray = Expression.NewArrayInit(typeof(object), arguments);
      var parameters = new Expression[] { lambda, argumentArray };
      return Expression.Call(null, EctRuntime.InvokeMethod, parameters);// parameters);
    }

    private Expression ParseCallExpression(bool callByMe = false)
    {
      Expression me = ParseMemberExpression();
      if (me != null)
      {
        var a = ParseArguments();
        if (a != null)
        {
          return MakeInvokeExpression(me, a);
        }
        return me;
      }
      if (callByMe)
      {
        return null;
      }
      Expression ce = ParseCallExpression(true);
      if (ce == null)
      {
        return null;
      }
      for (;;)
      {
        var e1 = ParseArguments();
        if (e1 != null)
        {
          ce = MakeInvokeExpression(ce, e1);
          continue;
        }
        var e2 = ParsePropertyOperator(ce);
        if (e2 != null)
        {
          ce = e2;
          continue;
        }
        return ce;
      }
    }


    private Expression ParseLeftHandSideExpression()
    {
      return ParseCallExpression();
    }

    private Expression ParsePostfixExpression(Expression leftHandside = null)
    {
      var left = leftHandside ?? ParseLeftHandSideExpression();
      if (left == null)
      {
        return null;
      }
      var op = lexer.CurrentToken;
      if (op != EctToken.Inc && op != EctToken.Dec)
      {
        return left;
      }
      lexer.NextToken();

      switch (op)
      {
        case EctToken.Inc:
          return Expression.Increment(left);
        case EctToken.Dec:
          return Expression.Decrement(left);
        default:
          throw new InvalidProgramException();
      }
    }

    private Expression ParseUnaryExpression(Expression leftHandside = null)
    {
      var op = lexer.CurrentToken;
      if (op != EctToken.Inc && op != EctToken.Dec &&
          op != EctToken.Add && op != EctToken.Sub &&
          op != EctToken.Not)
      {
        return ParsePostfixExpression(leftHandside);
      }
      lexer.NextToken();
      var unary = ParseUnaryExpression();
      if (unary == null)
      {
        return null;
      }
      switch (op)
      {
        case EctToken.Inc:
          return Expression.Increment(unary);
        case EctToken.Dec:
          return Expression.Decrement(unary);
        case EctToken.Add:
          return unary;
        case EctToken.Sub:
          return Expression.Multiply(Expression.Constant(-1), unary);
        case EctToken.Not:
          return Expression.Not(unary);
        default:
          throw new InvalidProgramException();
      }
    }

    private Expression ParseMultiplicativeExpression(Expression leftHandside = null)
    {
      var mult = ParseUnaryExpression(leftHandside);
      if (mult == null)
      {
        return null;
      }
      for (;;)
      {
        var op = lexer.CurrentToken;
        if (op != EctToken.Mul && op != EctToken.Div && op != EctToken.Mod)
        {
          return mult;
        }
        lexer.NextToken();

        var unaray = ParseUnaryExpression();

        switch (op)
        {
          case EctToken.Mul:
            mult = Expression.Multiply(mult, unaray);
            break;
          case EctToken.Div:
            mult = Expression.Divide(mult, unaray);
            break;
          case EctToken.Mod:
            mult = Expression.Modulo(mult, unaray);
            break;
          default:
            throw new InvalidProgramException();
        }
      }
    }

    private Expression ParseAdditiveExpression(Expression leftHandside = null)
    {
      var additive = ParseMultiplicativeExpression(leftHandside);
      if (additive == null)
      {
        return null;
      }
      for (;;)
      {
        var op = lexer.CurrentToken;
        if (op != EctToken.Add && op != EctToken.Sub)
        {
          return additive;
        }
        lexer.NextToken();

        var mult = ParseMultiplicativeExpression();

        additive = op == EctToken.Add ? Expression.Add(additive, mult) : Expression.Subtract(additive, mult);
      }
    }

    private Expression ParseShiftExpression(Expression leftHandside = null)
    {
      return ParseAdditiveExpression(leftHandside);
    }

    private Expression ParseRelationalExpression(Expression leftHandside = null)
    {
      var relation = ParseShiftExpression(leftHandside);
      if (relation == null)
      {
        return null;
      }
      for (;;)
      {
        var op = lexer.CurrentToken;
        if (op != EctToken.Le && op != EctToken.Lt &&
            op != EctToken.Ge && op != EctToken.Gt)
        {
          return relation;
        }
        lexer.NextToken();

        var shift = ParseShiftExpression();

        switch (op)
        {
          case EctToken.Le:
            relation = Expression.LessThanOrEqual(relation, shift);
            break;
          case EctToken.Lt:
            relation = Expression.LessThan(relation, shift);
            break;
          case EctToken.Ge:
            relation = Expression.GreaterThanOrEqual(relation, shift);
            break;
          case EctToken.Gt:
            relation = Expression.GreaterThan(relation, shift);
            break;
          default:
            throw new InvalidProgramException();
        }
      }
    }

    private Expression ParseEqualityExpression(Expression leftHandside = null)
    {
      var equality = ParseRelationalExpression(leftHandside);
      if (equality == null)
      {
        return null;
      }
      for (;;)
      {
        var op = lexer.CurrentToken;
        if (op != EctToken.Eq && op != EctToken.Ne)
        {
          return equality;
        }
        lexer.NextToken();

        var relational = ParseRelationalExpression();

        equality = op == EctToken.Eq ? Expression.Equal(equality, relational) : Expression.NotEqual(equality, relational);
      }
    }

    private Expression ParseBitwiseOrExpression(Expression leftHandside = null)
    {
      return ParseEqualityExpression(leftHandside);
    }

    private Expression ParseLogicalExpression(Expression leftHandside = null)
    {
      var logical = ParseBitwiseOrExpression(leftHandside);
      if (logical == null)
      {
        return null;
      }
      for (;;)
      {
        var op = lexer.CurrentToken;
        if (op != EctToken.And && op != EctToken.Or)
        {
          return logical;
        }
        lexer.NextToken();

        var bitwise = ParseBitwiseOrExpression();

        logical = op == EctToken.And ? Expression.And(logical, bitwise) : Expression.Or(logical, bitwise);
      }
    }

    private Expression ParseConditionalExpression(Expression leftHandside = null)
    {
      var e = ParseLogicalExpression(leftHandside);
      if (e == null)
      {
        return null;
      }
      if (lexer.CurrentToken != EctToken.Question)
      {
        return e;
      }
      lexer.NextToken();

      var ifTrue = ParseAssignmentExpression();

      if (!CheckCurrentToken(EctToken.Collon))
      {
        return null;
      }
      lexer.NextToken();

      var ifFalse = ParseAssignmentExpression();
      return Expression.IfThenElse(e, ifTrue, ifFalse);
    }

    private readonly static EctToken[] CompoundAssignmentOperator = new EctToken[]
    {
      EctToken.Let, EctToken.LetMul,EctToken.LetDiv,EctToken.LetMod,
      EctToken.LetAdd,EctToken.LetSub
    };

    private Expression ParseAssignmentExpression()
    {
      var left = ParseLeftHandSideExpression();
      if (left == null)
      {
        return null;
      }

      var ce = ParseConditionalExpression(left);
      if (left != ce)
      {
        return ce;
      }

      var op = lexer.CurrentToken;
      if (!CompoundAssignmentOperator.Contains(op))
      {
        return ce;
      }
      lexer.NextToken();

      var right = ParseAssignmentExpression();
      if (right == null)
      {
        return null;
      }

      switch (op)
      {
        case EctToken.Let:
          return Expression.Assign(left, right);
        case EctToken.LetMul:
          return Expression.MultiplyAssign(left, right);
        case EctToken.LetDiv:
          return Expression.DivideAssign(left, right);
        case EctToken.LetMod:
          return Expression.ModuloAssign(left, right);
        case EctToken.LetAdd:
          return Expression.AddAssign(left, right);
        case EctToken.LetSub:
          return Expression.SubtractAssign(left, right);
        default:
          throw new InvalidProgramException();
      }
    }

    // ParseCommaExpression

    private Expression ParsePropertyOperator(Expression e)
    {
      var op = lexer.CurrentToken;
      switch (op)
      {
        case EctToken.Dot:
        case EctToken.QuestionDot:
          lexer.NextToken();
          if (!CheckCurrentToken(EctToken.Ident))
          {
            return null;
          }
          var ident = lexer.CurrentValue;
          lexer.NextToken();
          return Getter(e, ident, op == EctToken.QuestionDot);
        
        //case EctToken.ArrayStart:
        //  ParseBrackets(e);
        //  break;
        default:
          return null;
      }
    }

    private Expression ParseThisExpression()
    {
      var t = lexer.NextToken();
      if (!CheckCurrentToken(EctToken.Ident))
      {
        return null;
      }
      var ident = lexer.CurrentValue;
      lexer.NextToken();
      return Getter(self, ident);
    }

    private Expression Getter(Expression target, string propertyName, bool allowNull = false)
    {
      var getter = Expression.Call(null, EctRuntime.GetterMethod, target, Expression.Constant(propertyName));
      if (allowNull)
      {
        var resultVariable = Expression.Variable(typeof(object));
        var tempVariable = Expression.Variable(typeof(object));
        var nullValue = Expression.Constant(null, typeof(object));
        var nullCheck = Expression.NotEqual(tempVariable, nullValue);

        return Expression.Block
          (
            Expression.Assign(resultVariable, nullValue),
            Expression.Assign(tempVariable, target),
            Expression.IfThen(nullCheck, Expression.Assign(resultVariable,getter)),
            resultVariable
          );
      }
      else
      {
        return getter;
      }
      //var arg = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null);
      //var getBinder = RuntimeBinder.GetMember(CSharpBinderFlags.None, propertyName, typeof(EctCompiler), new[] { arg });
      //return Expression.Dynamic(getBinder, typeof(object), target);
    }

    public Expression ParseInclude()
    {
      lexer.NextToken();
      if (!CheckCurrentToken(EctToken.Literal_String))
      {
        return null;
      }
      var template = lexer.CurrentValue;
      lexer.NextToken();
      return Expression.Call(context, EctRuntimeContext.IncludeMethod, Expression.Constant(template), Expression.Constant(null));
    }

    private void ParseIndent()
    {
      var t = lexer.CurrentToken;
      if (t == EctToken.Collon)
      {
        Debug.WriteLine("***** Skin Indent Syntax");
        lexer.NextToken();
      }
    }

    private bool CheckCurrentToken(params EctToken[] expected)
    {
      if (expected == null || expected.Length == 0)
      {
        throw new ArgumentNullException("expected");
      }

      var actual = lexer.CurrentToken;
      if (expected.Contains(actual))
      {
        return true;
      }
      var actualValue = lexer.CurrentValue;

      var s = string.Join("|", expected);
      LogError($"'{s}' was expected, but Type:'{actual}',Value:'{actualValue}' is actual.");

      FindErrorResume();

      return false;
    }

    private void SyntaxError(string msg)
    {
      LogError(msg);

      FindErrorResume();
    }

    private void UnknownTokenError()
    {
      LogError("Unknow token error");

      FindErrorResume();
    }

    private void FindErrorResume()
    {
      if (lexer.CurrentToken == EctToken.Keyword_End ||
         lexer.CurrentToken == EctToken.Html)
      {
        lexer.NextToken();
      }

      for (;;)
      {
        switch (lexer.CurrentToken)
        {
          case EctToken.Eof:
            return;
          case EctToken.Keyword_End:
          case EctToken.Html:
            return;
          default:
            break;
        }
        lexer.NextToken();
      }
    }

    private void LogError(string message)
    {
      var e = new EctError
      {
        TemplateName = lexer.TemplateName,
        LinePos = lexer.LineNumber,
        ColumnPos = lexer.ColNumber,
        Message = message
      };
      errors.Add(e);
      Debug.WriteLine("Parse error: {0}", e, null);

    }
  }
}

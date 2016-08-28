using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ectcs
{
  public class EctRuntimeContext
  {
    public static readonly MethodInfo IncludeMethod = typeof(EctRuntimeContext).GetMethod(nameof(Include));
    public static readonly MethodInfo EscapeMethod = typeof(EctRuntimeContext).GetMethod(nameof(Escape));
    public static readonly MethodInfo UnescapeMethod = typeof(EctRuntimeContext).GetMethod(nameof(Unescape));
    public static readonly MethodInfo DefineBlockMethod = typeof(EctRuntimeContext).GetMethod(nameof(DefineBlock));
    public static readonly MethodInfo CallBlockMethod = typeof(EctRuntimeContext).GetMethod(nameof(CallBlock));

    private StringBuilder output = new StringBuilder();
    private Ect ect;
    private object self;
    private Dictionary<string, LambdaExpression> blocks = new Dictionary<string, LambdaExpression>();

    public EctRuntimeContext(Ect ect, object self)
    {
      this.ect = ect;
      this.self = self;
    }

    public string Rendered => output.ToString();

    public void DefineBlock(string name, LambdaExpression lambda)
    {
      blocks[name] = lambda;
    }

    public LambdaExpression CallBlock(string name)
    {
      return blocks[name];
    }

    public void Include(string name, object argument)
    {
      var saved = this.self;
      {
        this.self = argument ?? saved;
        var t = ect.Get(name);
        t(this, this.self);
      }
      this.self = saved;
    }

    public void Escape(object s)
    {
      output.Append(WebUtility.HtmlEncode(ToString(s)));
    }

    public void Unescape(object s)
    {
      output.Append(ToString(s));
    }

    private string ToString(object o) => o == null ? "(null)" : o.ToString();
  }
}

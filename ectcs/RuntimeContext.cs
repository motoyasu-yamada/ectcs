using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ectcs
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
    private Dictionary<string, Expression<Action<EctRuntimeContext, object>>> blocks = new Dictionary<string, Expression<Action<EctRuntimeContext, object>>>();

    private static readonly Expression<Action<EctRuntimeContext, object>> emptyBlock = (c, o) => Debug.WriteLine("*** Empty Block ***");

    public EctRuntimeContext(Ect ect, object self)
    {
      this.ect = ect;
      this.self = self;
    }

    public string Rendered => output.ToString();

    public void DefineBlock(string name, Expression<Action<EctRuntimeContext, object>> lambda)
    {
      Debug.WriteLine("+++++ DefineBlock: {0}, {1}", name, lambda);
      blocks[name] = lambda;
    }

    public Expression<Action<EctRuntimeContext, object>> CallBlock(string name)
    {
      Debug.WriteLine("+++++ CallBlock: {0}", name, null);

      Expression<Action<EctRuntimeContext, object>> block = null;
      if (!blocks.TryGetValue(name, out block))
      {
        block = emptyBlock;
      }
      return block;
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

    private string ToString(object o)
    {
      if (o == null)
      {
        return "";
      }
      else
      {
        return o.ToString();
      }
    }
  }
}

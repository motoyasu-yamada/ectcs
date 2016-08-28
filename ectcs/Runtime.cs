using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ectcs
{
  public static class EctRuntime
  {
    public static readonly MethodInfo GetterMethod = typeof(EctRuntime).GetMethod(nameof(Getter));
    public static readonly MethodInfo TestMethod = typeof(EctRuntime).GetMethod(nameof(Test));
    public static readonly MethodInfo InvokeMethod = typeof(EctRuntime).GetMethod(nameof(Invoke));

    public static dynamic Add(dynamic x, dynamic y)
    {
      return x + y;
    }

    public static bool Test(object test)
    {
      bool r;
      if (test == null)
      {
        r = false;
        goto finish;
      }
      if (test is bool)
      {
        r = (bool)test;
        goto finish;
      }
      if (test is string)
      {
        r = String.IsNullOrEmpty(test as string);
        goto finish;
      }
      if (test is Array)
      {
        r = (test as Array).Length != 0;
        goto finish;
      }
      if (test is IEnumerable)
      {
        r = (test as IEnumerable).GetEnumerator().MoveNext();
        goto finish;
      }
      if (test is char)
      {
        r = (char)test != 0;
        goto finish;
      }
      if (test is byte)
      {
        r = (byte)test != 0;
        goto finish;
      }
      if (test is sbyte)
      {
        r = (sbyte)test != 0;
        goto finish;
      }
      if (test is int)
      {
        r = (int)test != 0;
        goto finish;
      }
      if (test is long)
      {
        r = (long)test != 0;
        goto finish;
      }
      if (test is float)
      {
        r = (float)test != 0;
        goto finish;
      }
      if (test is double)
      {
        r = (double)test != 0;
        goto finish;
      }

      r = true;
finish:
      return r;
    }


    public static object Getter(object target, string name)
    {
      object value;
      if (target is Dictionary<string, object>)
      {
        var d = target as Dictionary<string, object>;
        value = d[name];
        goto finish;
      }
      var p = target.GetType().GetProperty(name);
      if (p != null)
      {
        value = p.GetValue(target);
        goto finish;
      }
      var f = target.GetType().GetProperty(name);
      if (f != null)
      {
        value = f.GetValue(target);
        goto finish;
      }
      // TODO: as options, avoid exception
      throw new ArgumentException($"Member '{name}' doesn't exist in object'{target}'");
finish:
//Debug.WriteLine("{0} \n-> {1} \n= {2}", VarDump.ToString(target), name, VarDump.ToString(value));
      return value;
    }

    public static object Invoke(object lambda, object[] parameters /*params object[] arguments*/)
    {
      if (lambda == null)
      {
        throw new ArgumentException("lambda");
      }
      if (parameters == null)
      {
        throw new ArgumentException("parameters");
      }

      var f = lambda as Delegate;
      return f.DynamicInvoke(parameters);
      //var type = lambda.GetType();
      //Debug.WriteLine(">>>>> InvokeERROR:\n Type: {0}, Object: {1}", type, lambda);
      //foreach (var p in parameters)
      //{
      //  var ptype = p.GetType();
      //  Debug.WriteLine("\n: Type: {0}, Object: {1}", ptype, p);
      //}
      //Debug.WriteLine("<<<<<");
      //return "********************";
    }
  }
}

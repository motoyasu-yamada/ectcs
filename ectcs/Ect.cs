using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ectcs
{
  public class Ect
  {
    private static readonly Regex driverLetterExp = new Regex("/^[a-zA-Z]:");

    private Dictionary<string, Action<EctRuntimeContext, object>> cache = new Dictionary<string, Action<EctRuntimeContext, object>>();
    private Dictionary<string, string> watchers = new Dictionary<string, string>();

    private EctOptions options;

    public Ect(EctOptions options = null)
    {
      this.options = new EctOptions(options) ?? new EctOptions();
    }

    public string Render(string path, object value)
    {
      var c = new EctRuntimeContext(this, value);
      var t = Get(path);
      t(c, value);
      return c.Rendered;
    }

    public Action<EctRuntimeContext, object> Get(string path)
    {
      Action<EctRuntimeContext, object> c;
      if (TryGetCache(path, out c))
      {
        return c;
      }
      var rawPath = ResolveTemplateFile(path);
      var data = Read(rawPath);

      var lexer = new EctLexer(path, data, 0, data.Length, options);
      var compiler = new EctCompiler(lexer);
      c = compiler.Compile();

      TrySetCache(path, rawPath, c);
      return c;
    }

    private bool TryGetCache(string template, out Action<EctRuntimeContext, object> cached)
    {
      cached = null;
      if (options.Cache && cache.TryGetValue(template, out cached))
      {
        return true;
      }
      return false;
    }

    private void TrySetCache(string template, string file, Action<EctRuntimeContext, object> tocache)
    {
      if (!options.Cache)
      {
        return;
      }
      cache[template] = tocache;
      if (!options.Watch)
      {
        return;
      }
      if (watchers.ContainsKey(file))
      {
        return;
      }
      //watchers[file] = fs.watch(file, new { persistent = false }, () =>
      //{
      //  watchers[file].close();
      //  delete(watchers[file]);
      //  delete(cache[template]);
      //});
    }

    private string ResolveTemplateFile(string template)
    {
      if (template == null)
      {
        throw new ArgumentNullException("template");
      }

      string file;
      if (String.IsNullOrEmpty(options.RootPath))
      {
        file = NormalizePath((IsAbsolutePath(template) ? "" : (options.RootPath + "/")) + options.ExtRegex.Replace(template, "") + options.Ext);
      }
      else
      {
        file = template;
      }
      return file;
    }

    private static bool IsAbsolutePath(string path)
    {
      var c = path[0];
      if (IsWindows)
      {
        return c == '/' || c == '\\' || driverLetterExp.IsMatch(path);
      }
      else
      {
        return c == '/';
      }
    }

    private static string NormalizePath(string src)
    {
      return src;
    }

    private static bool? isWindows;
    private static bool IsWindows
    {
      get
      {
        if (!isWindows.HasValue)
        {
          switch (Environment.OSVersion.Platform)
          {
            case PlatformID.Win32NT:
            case PlatformID.Win32S:
            case PlatformID.Win32Windows:
            case PlatformID.WinCE:
              isWindows = true;
              break;
            default:
              isWindows = false;
              break;
          }
        }
        return isWindows.Value;
      }
    }

    public string Read(string file)
    {
      if (options.OnMemory != null)
      {
        return options.OnMemory[file];
      }

      try
      {
        return File.ReadAllText(file, Encoding.UTF8);
      }
      catch (Exception e)
      {
        throw new Exception($"Failed to load template '{file}'", e);
      }
    }
  }
}

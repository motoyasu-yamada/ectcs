using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ectcs
{
  /*
  * EctOptions
  */
  public class EctOptions
  {
    public EctOptions() { Ext = ""; }

    public EctOptions(EctOptions src) : this()
    {
      if (src == null)
      {
        return;
      }
      Open = src.Open;
      Close = src.Close;
      Ext = src.Ext;
      Cache = src.Cache;
      Watch = src.Watch;
      RootPath = src.RootPath;
      OnMemory = src.OnMemory;
      DebugOutput = src.DebugOutput;
    }

    public string Open { get; set; } = "<%";
    public string Close { get; set; } = "%>";

    private string ext;
    public string Ext
    {
      get { return ext; }
      set
      {
        if (value == null)
        {
          throw new ArgumentNullException("value");
        }
        ExtRegex = new Regex(Regex.Escape(value) + "$");
        ext = value;
      }
    }

    public Regex ExtRegex { get; private set; }
    public bool Cache { get; set; } = true;
    public bool Watch { get; set; } = false;
    public string RootPath { get; set; } = null;
    public EctTemplateOnMemory OnMemory { get; set; } = null;
    public bool DebugOutput { get; set; } = false;
  }
}

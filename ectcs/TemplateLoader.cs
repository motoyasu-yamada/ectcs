using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ectcs
{
  /**
   */
  public class EctTemplateOnMemory
  {
    private readonly static Regex pathCheck = new Regex("^\\/(?:[A-Za-z0-9]+)(?:\\.(?:[A-Za-z0-9]+))*$");

    private Dictionary<string, string> templates = new Dictionary<string, string>();

    public string this[string index]
    {
      get
      {
        string value;
        if (templates.TryGetValue(index, out value))
        {
          return value;
        }
        throw new Exception($"Failed to load template. '{index}' doesn't exist on memory.");
      }

      set
      {
        if (String.IsNullOrEmpty(index))
        {
          throw new ArgumentNullException("index");
        }
        if (!pathCheck.IsMatch(index))
        {
          throw new ArgumentException($"Invalid character is used: index: '{index}'");
        }
        if (value == null)
        {
          throw new ArgumentNullException("value", $"Tried to set null to index: '{index}'");
        }
        templates[index] = value;
      }
    }
  }
}

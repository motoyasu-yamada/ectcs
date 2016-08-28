using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ectcs
{
  public sealed class EctError
  {
    public string TemplateName { get; set; }
    public int LinePos { get; set; }
    public int ColumnPos { get; set; }
    public string Message { get; set; }
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CERVED_Service;

public class CervedErrorApiResponse
{
    public int status { get; set; }
    public int serviceError { get; set; }
    public string statusDescription { get; set; }
    public string serviceErrorDescription { get; set; }
}

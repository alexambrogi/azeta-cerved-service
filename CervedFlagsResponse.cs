using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CERVED_Service;

public class CervedFlagsResponse
{
    public CompanyFlags[] companies { get; set; }
    public PersonFlags[] people { get; set; }
}

public class CompanyFlags
{
    public int subjectId { get; set; }
    public string taxCode { get; set; }
    public bool protests { get; set; }
    public bool prejudicialEvents { get; set; }
    public bool procedures { get; set; }
    public bool crisisEvents { get; set; }
    public bool cigs { get; set; }
    public bool personalBankruptcies { get; set; }
    public string name { get; set; }
    public string vatNumber { get; set; }
}

public class PersonFlags
{
    public int subjectId { get; set; }
    public string taxCode { get; set; }
    public bool protests { get; set; }
    public bool prejudicialEvents { get; set; }
    public bool procedures { get; set; }
    public bool crisisEvents { get; set; }
    public bool cigs { get; set; }
    public bool personalBankruptcies { get; set; }
}

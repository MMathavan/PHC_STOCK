using HMS_STOCK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMS_STOCK.Models
{
    public class Company
    {
        public List<HMS_STOCK.Models.CompanyMaster> masterdata { get; set; }
        public List<HMS_STOCK.Models.CompanyDetail> detaildata { get; set; }
        //public List<pr_CompanyDetail_Flx_Assgn_Result> queryresultdata { get; set; }
    }
}
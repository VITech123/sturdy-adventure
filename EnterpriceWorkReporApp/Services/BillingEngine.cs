
using System;
using System.Data;

namespace EnterpriseWorkReport.Services
{
 public class BillingEngine
 {
  public double Calculate(string formula)
  {
   DataTable t=new DataTable();
   var r=t.Compute(formula,"");
   return Convert.ToDouble(r);
  }
 }
}

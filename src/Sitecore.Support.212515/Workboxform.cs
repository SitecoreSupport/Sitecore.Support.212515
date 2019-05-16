using Sitecore.Text;
using Sitecore.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Shell.Applications.Workbox
{
  public class Workboxform : Sitecore.Shell.Applications.Workbox.WorkboxForm
  {
    protected override void Refresh()
    {
      this.Refresh(null);
    }
    protected new void Refresh(Dictionary<string, string> urlArguments)
    {
      UrlString urlString = new UrlString(WebUtil.GetRawUrl());
      urlString["reload"] = "1";
      if (urlArguments != null)
      {
        foreach (KeyValuePair<string, string> urlArgument in urlArguments)
        {
          urlString[urlArgument.Key] = urlArgument.Value;
        }
      }
      Context.ClientPage.ClientResponse.SetLocation(urlString.ToString());
    }
  }
}
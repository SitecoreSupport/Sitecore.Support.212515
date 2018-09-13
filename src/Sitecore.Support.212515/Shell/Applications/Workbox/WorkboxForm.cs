using Sitecore.Text;
using Sitecore.Web;
using System.Collections.Generic;

namespace Sitecore.Support.Shell.Applications.Workbox
{
  public class WorkboxForm: Sitecore.Shell.Applications.Workbox.WorkboxForm
  {
    protected override void Refresh()
    {
      Refresh(null);
    }

    protected new void Refresh(Dictionary<string, string> urlArguments)
    {
      var url = new UrlString(WebUtil.GetRawUrl());
      url["reload"] = "1";

      if (urlArguments != null)
      {
        foreach (var urlArgument in urlArguments)
        {
          url[urlArgument.Key] = urlArgument.Value;
        }
      }

      Context.ClientPage.ClientResponse.SetLocation(url.ToString());
    }
  }
}

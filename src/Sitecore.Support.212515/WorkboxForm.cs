namespace Sitecore.Support.Shell.Applications.Workbox
{
    using Sitecore;
    using Sitecore.Collections;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Exceptions;
    using Sitecore.Globalization;
    using Sitecore.Pipelines;
    using Sitecore.Pipelines.GetWorkflowCommentsDisplay;
    using Sitecore.Resources;
    using Sitecore.Shell.Applications.Workbox;
    using Sitecore.Shell.Data;
    using Sitecore.Shell.Feeds;
    using Sitecore.Shell.Framework;
    using Sitecore.Shell.Framework.CommandBuilders;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls.Ribbons;
    using Sitecore.Web.UI.XmlControls;
    using Sitecore.Workflows;
    using Sitecore.Workflows.Simple;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.UI;

    /// <summary>
    /// Displays the workbox.
    /// </summary>
    public class WorkboxForm : BaseForm
    {
        private class OffsetCollection
        {
            public int this[string key]
            {
                get
                {
                    if (Context.ClientPage.ServerProperties[key] != null)
                    {
                        return (int)Context.ClientPage.ServerProperties[key];
                    }
                    UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                    if (urlString[key] != null)
                    {
                        int result;
                        if (!int.TryParse(urlString[key], out result))
                        {
                            return 0;
                        }
                        return result;
                    }
                    return 0;
                }
                set
                {
                    Context.ClientPage.ServerProperties[key] = value;
                }
            }
        }

        /// <summary>
        /// Holds items for a workflow state.
        /// </summary>
        protected class StateItems
        {
            /// <summary>
            /// Gets or sets the items for the state.
            /// </summary>
            public IEnumerable<Item> Items
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the command IDs for the state.
            /// </summary>
            public IEnumerable<string> CommandIds
            {
                get;
                set;
            }
        }

        /// <summary>
        /// The pager.
        /// </summary>
        protected Border Pager;

        /// <summary>
        /// The ribbon panel.
        /// </summary>
        protected Border RibbonPanel;

        /// <summary>
        /// The states.
        /// </summary>
        protected Border States;

        /// <summary>
        /// The view menu.
        /// </summary>
        protected Toolmenubutton ViewMenu;

        /// <summary>
        /// The _state names.
        /// </summary>
        private NameValueCollection stateNames;

        /// <summary>
        /// The maximum length allowed for a comment text.
        /// </summary>
        private readonly int CommentMaxLength = 2000;

        /// <summary>
        /// Gets or sets the offset(what page we are on).
        /// </summary>
        /// <value>The size of the offset.</value>
        private OffsetCollection Offset = new OffsetCollection();

        /// <summary>
        /// Gets or sets the size of the page.
        /// </summary>
        /// <value>The size of the page.</value>
        public int PageSize
        {
            get
            {
                return Registry.GetInt("/Current_User/Workbox/Page Size", 10);
            }
            set
            {
                Registry.SetInt("/Current_User/Workbox/Page Size", value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether page is reloads by reload button on the ribbon.
        /// </summary>
        /// <value><c>true</c> if this instance is reload; otherwise, <c>false</c>.</value>
        protected virtual bool IsReload
        {
            get
            {
                UrlString urlString = new UrlString(WebUtil.GetRawUrl());
                return urlString["reload"] == "1";
            }
        }

        /// <summary>
        /// Comments the specified args.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        public void Comment(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            object obj = Context.ClientPage.ServerProperties["items"];
            Assert.IsNotNull(obj, "Items is null");
            List<ItemUri> itemUris = (List<ItemUri>)obj;
            ID result = ID.Null;
            if (Context.ClientPage.ServerProperties["command"] != null)
            {
                ID.TryParse(Context.ClientPage.ServerProperties["command"] as string, out result);
            }
            bool flag = (args.Parameters["ui"] != null && args.Parameters["ui"] == "1") || (args.Parameters["suppresscomment"] != null && args.Parameters["suppresscomment"] == "1");
            if (!args.IsPostBack && result != (ID)null && !flag)
            {
                DisplayCommentDialog(itemUris, result, args);
            }
            else if ((args.Result != null && args.Result != "null" && args.Result != "undefined" && args.Result != "cancel") || flag)
            {
                string result2 = args.Result;
                Sitecore.Collections.StringDictionary stringDictionary = new Sitecore.Collections.StringDictionary();
                string workflowStateId = string.Empty;
                if (Context.ClientPage.ServerProperties["workflowStateid"] != null)
                {
                    workflowStateId = Context.ClientPage.ServerProperties["workflowStateid"].ToString();
                }
                string command = Context.ClientPage.ServerProperties["command"].ToString();
                IWorkflow workflowFromPage = GetWorkflowFromPage();
                if (workflowFromPage != null)
                {
                    if (!string.IsNullOrEmpty(result2))
                    {
                        stringDictionary = WorkflowUIHelper.ExtractFieldsFromFieldEditor(result2);
                    }
                    if (!string.IsNullOrWhiteSpace(stringDictionary["Comments"]) && stringDictionary["Comments"].Length > CommentMaxLength)
                    {
                        Context.ClientPage.ClientResponse.Alert(string.Format("The comment is too long.\n\nYou have entered {0} characters.\nA comment cannot contain more than 2000 characters.", stringDictionary["Comments"].Length));
                        DisplayCommentDialog(itemUris, result, args);
                    }
                    else
                    {
                        ExecutCommand(itemUris, workflowFromPage, stringDictionary, command, workflowStateId);
                        Refresh();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            switch (message.Name)
            {
                case "workflow:send":
                    Send(message);
                    return;
                case "workflow:sendselected":
                    SendSelected(message);
                    return;
                case "workflow:sendall":
                    SendAll(message);
                    return;
                case "window:close":
                    Sitecore.Shell.Framework.Windows.Close();
                    return;
                case "workflow:showhistory":
                    ShowHistory(message, Context.ClientPage.ClientRequest.Control);
                    return;
                case "workbox:hide":
                    Context.ClientPage.SendMessage(this, "pane:hide(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "false");
                    break;
                case "pane:hidden":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "false");
                    break;
                case "workbox:show":
                    Context.ClientPage.SendMessage(this, "pane:show(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "true");
                    break;
                case "pane:showed":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "true");
                    break;
            }
            base.HandleMessage(message);
            string text = message["id"];
            if (!string.IsNullOrEmpty(text))
            {
                string @string = StringUtil.GetString(message["language"]);
                string string2 = StringUtil.GetString(message["version"]);
                Item item = Context.ContentDatabase.Items[text, Language.Parse(@string), Sitecore.Data.Version.Parse(string2)];
                if (item != null)
                {
                    Dispatcher.Dispatch(message, item);
                }
            }
        }

        /// <summary>
        /// Diffs the specified id.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Diff(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            UrlString urlString = new UrlString(UIUtil.GetUri("control:Diff"));
            urlString.Append("id", id);
            urlString.Append("la", language);
            urlString.Append("vs", version);
            urlString.Append("wb", "1");
            Context.ClientPage.ClientResponse.ShowModalDialog(urlString.ToString());
        }

        /// <summary>
        /// Displays the state.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="state">
        /// The state.
        /// </param>
        /// <param name="stateItems">
        /// The item for the workflow state.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="pageSize">
        /// Size of the page.
        /// </param>    
        protected virtual void DisplayState(IWorkflow workflow, WorkflowState state, StateItems stateItems, System.Web.UI.Control control, int offset, int pageSize)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(stateItems, "stateItems");
            Assert.ArgumentNotNull(control, "control");
            Item[] array = stateItems.Items.ToArray();
            if (array.Length > 0)
            {
                int num = offset + pageSize;
                if (num > array.Length)
                {
                    num = array.Length;
                }
                for (int i = offset; i < num; i++)
                {
                    CreateItem(workflow, array[i], control);
                }
                Border border = new Border();
                border.Background = "#fff";
                Border border2 = border;
                control.Controls.Add(border2);
                border2.Margin = "0 5px 10px 15px";
                border2.Padding = "5px 10px";
                border2.Class = "scWorkboxToolbarButtons";
                WorkflowCommand[] commands = workflow.GetCommands(state.StateID);
                WorkflowCommand[] array2 = commands;
                foreach (WorkflowCommand workflowCommand in array2)
                {
                    if (stateItems.CommandIds.Contains(workflowCommand.CommandID))
                    {
                        XmlControl xmlControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                        Assert.IsNotNull(xmlControl, "workboxCommand is null");
                        xmlControl["Header"] = workflowCommand.DisplayName + " " + Translate.Text("(selected)");
                        xmlControl["Icon"] = workflowCommand.Icon;
                        xmlControl["Command"] = "workflow:sendselected(command=" + workflowCommand.CommandID + ",ws=" + state.StateID + ",wf=" + workflow.WorkflowID + ")";
                        border2.Controls.Add(xmlControl);
                        xmlControl = (Resource.GetWebControl("WorkboxCommand") as XmlControl);
                        Assert.IsNotNull(xmlControl, "workboxCommand is null");
                        xmlControl["Header"] = workflowCommand.DisplayName + " " + Translate.Text("(all)");
                        xmlControl["Icon"] = workflowCommand.Icon;
                        xmlControl["Command"] = "workflow:sendall(command=" + workflowCommand.CommandID + ",ws=" + state.StateID + ",wf=" + workflow.WorkflowID + ")";
                        border2.Controls.Add(xmlControl);
                    }
                }
            }
        }

        /// <summary>
        /// Displays the states.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="placeholder">
        /// The placeholder.
        /// </param>
        protected virtual void DisplayStates(IWorkflow workflow, XmlControl placeholder)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(placeholder, "placeholder");
            stateNames = null;
            WorkflowState[] states = workflow.GetStates();
            foreach (WorkflowState workflowState in states)
            {
                StateItems stateItems = GetStateItems(workflowState, workflow);
                Assert.IsNotNull(stateItems, "stateItems is null");
                if (stateItems.CommandIds.Any())
                {
                    string str = ShortID.Encode(workflow.WorkflowID) + "_" + ShortID.Encode(workflowState.StateID);
                    Section section = new Section();
                    section.ID = str + "_section";
                    Section section2 = section;
                    placeholder.AddControl(section2);
                    int num = stateItems.Items.Count();
                    string arg = (num <= 0) ? Translate.Text("None") : ((num != 1) ? string.Format("{0} {1}", num, Translate.Text("items")) : string.Format("1 {0}", Translate.Text("item")));
                    arg = $"<span style=\"font-weight:normal\"> - ({arg})</span>";
                    section2.Header = workflowState.DisplayName + arg;
                    section2.Icon = workflowState.Icon;
                    if (Settings.ClientFeeds.Enabled)
                    {
                        FeedUrlOptions feedUrlOptions = new FeedUrlOptions("/sitecore/shell/~/feed/workflowstate.aspx");
                        feedUrlOptions.UseUrlAuthentication = true;
                        FeedUrlOptions feedUrlOptions2 = feedUrlOptions;
                        feedUrlOptions2.Parameters["wf"] = workflow.WorkflowID;
                        feedUrlOptions2.Parameters["st"] = workflowState.StateID;
                        section2.FeedLink = feedUrlOptions2.ToString();
                    }
                    section2.Collapsed = (num <= 0);
                    Border border = new Border();
                    section2.Controls.Add(border);
                    border.ID = str + "_content";
                    DisplayState(workflow, workflowState, stateItems, border, Offset[workflowState.StateID], PageSize);
                    CreateNavigator(section2, str + "_navigator", num, Offset[workflowState.StateID]);
                }
            }
        }

        /// <summary>
        /// Displays the workflow.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        protected virtual void DisplayWorkflow(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Context.ClientPage.ServerProperties["WorkflowID"] = workflow.WorkflowID;
            XmlControl xmlControl = Resource.GetWebControl("Pane") as XmlControl;
            Error.AssertXmlControl(xmlControl, "Pane");
            States.Controls.Add(xmlControl);
            Assert.IsNotNull(xmlControl, "pane");
            xmlControl["PaneID"] = GetPaneID(workflow);
            xmlControl["Header"] = workflow.Appearance.DisplayName;
            xmlControl["Icon"] = workflow.Appearance.Icon;
            FeedUrlOptions feedUrlOptions = new FeedUrlOptions("/sitecore/shell/~/feed/workflow.aspx");
            feedUrlOptions.UseUrlAuthentication = true;
            FeedUrlOptions feedUrlOptions2 = feedUrlOptions;
            feedUrlOptions2.Parameters["wf"] = workflow.WorkflowID;
            xmlControl["FeedLink"] = feedUrlOptions2.ToString();
            DisplayStates(workflow, xmlControl);
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.Insert(States.ClientID, "append", HtmlUtil.RenderControl(xmlControl));
            }
        }

        /// <summary>
        /// Raises the load event.
        /// </summary>
        /// <param name="e">
        /// The <see cref="T:System.EventArgs" /> instance containing the event data.
        /// </param>
        /// <remarks>
        /// This method notifies the server control that it should perform actions common to each HTTP
        /// request for the page it is associated with, such as setting up a database query. At this
        /// stage in the page lifecycle, server controls in the hierarchy are created and initialized,
        /// view state is restored, and form controls reflect client-side data. Use the IsPostBack
        /// property to determine whether the page is being loaded in response to a client postback,
        /// or if it is being loaded and accessed for the first time.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow[] workflows = workflowProvider.GetWorkflows();
                    IWorkflow[] array = workflows;
                    foreach (IWorkflow workflow in array)
                    {
                        string str = "P" + Regex.Replace(workflow.WorkflowID, "\\W", string.Empty);
                        if (!IsReload && workflows.Length == 1 && string.IsNullOrEmpty(Registry.GetString("/Current_User/Panes/" + str)))
                        {
                            Registry.SetString("/Current_User/Panes/" + str, "visible");
                        }
                        if ((Registry.GetString("/Current_User/Panes/" + str) ?? string.Empty) == "visible")
                        {
                            DisplayWorkflow(workflow);
                        }
                    }
                }
                UpdateRibbon();
            }
            WireUpNavigators(Context.ClientPage);
        }

        /// <summary>
        /// Called when the view menu is clicked.
        /// </summary>
        protected void OnViewMenuClick()
        {
            Menu menu = new Menu();
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                IWorkflow[] workflows = workflowProvider.GetWorkflows();
                foreach (IWorkflow workflow in workflows)
                {
                    string paneID = GetPaneID(workflow);
                    string @string = Registry.GetString("/Current_User/Panes/" + paneID);
                    string str = (@string != "hidden") ? "workbox:hide" : "workbox:show";
                    menu.Add(Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl"), workflow.Appearance.DisplayName, workflow.Appearance.Icon, string.Empty, str + "(id=" + paneID + ")", @string != "hidden", string.Empty, MenuItemType.Check);
                }
                if (menu.Controls.Count > 0)
                {
                    menu.AddDivider();
                }
                menu.Add("Refresh", "Office/16x16/refresh.png", "Refresh");
            }
            Context.ClientPage.ClientResponse.ShowPopup("ViewMenu", "below", menu);
        }

        /// <summary>
        /// Opens the specified item.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Open(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            string sectionID = RootSections.GetSectionID(id);
            UrlString urlString = new UrlString();
            urlString.Append("ro", sectionID);
            urlString.Append("fo", id);
            urlString.Append("id", id);
            urlString.Append("la", language);
            urlString.Append("vs", version);
            Sitecore.Shell.Framework.Windows.RunApplication("Content editor", urlString.ToString());
        }

        /// <summary>
        /// Called with the pages size changes.
        /// </summary>
        protected void PageSize_Change()
        {
            string value = Context.ClientPage.ClientRequest.Form["PageSize"];
            int num = PageSize = MainUtil.GetInt(value, 10);
            Refresh();
        }

        /// <summary>
        /// Toggles the pane.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        protected void Pane_Toggle(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            string text = "P" + Regex.Replace(id, "\\W", string.Empty);
            string @string = Registry.GetString("/Current_User/Panes/" + text);
            if (Context.ClientPage.FindControl(text) == null)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider == null)
                {
                    return;
                }
                IWorkflow workflow = workflowProvider.GetWorkflow(id);
                DisplayWorkflow(workflow);
            }
            if (string.IsNullOrEmpty(@string) || @string == "hidden")
            {
                Registry.SetString("/Current_User/Panes/" + text, "visible");
                Context.ClientPage.ClientResponse.SetStyle(text, "display", string.Empty);
            }
            else
            {
                Registry.SetString("/Current_User/Panes/" + text, "hidden");
                Context.ClientPage.ClientResponse.SetStyle(text, "display", "none");
            }
            SheerResponse.SetReturnValue(true);
        }

        /// <summary>
        /// Previews the specified item.
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="version">
        /// The version.
        /// </param>
        protected void Preview(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            Context.ClientPage.SendMessage(this, "item:preview(id=" + id + ",language=" + language + ",version=" + version + ")");
        }

        /// <summary>
        /// Refreshes the page.
        /// </summary>
        protected virtual void Refresh()
        {
            Refresh(null);
        }

        /// <summary>
        /// Refreshes the page.
        /// </summary>
        /// <param name="urlArguments">The URL arguments.</param>
        protected void Refresh(Dictionary<string, string> urlArguments)
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
            #region Changed Code
            /* Original Code:
                string fullUrl = WebUtil.GetFullUrl(urlString.ToString());
                Context.ClientPage.ClientResponse.SetLocation(fullUrl);
             */
            Context.ClientPage.ClientResponse.SetLocation(urlString.ToString()); //Need to only set the relative path not full to avoid Https issues.
            #endregion
        }

        /// <summary>
        /// Shows the history.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        private static void ShowHistory(Message message, string control)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.ArgumentNotNull(control, "control");
            XmlControl xmlControl = Resource.GetWebControl("WorkboxHistory") as XmlControl;
            Assert.IsNotNull(xmlControl, "history is null");
            xmlControl["ItemID"] = message["id"];
            xmlControl["Language"] = message["la"];
            xmlControl["Version"] = message["vs"];
            xmlControl["WorkflowID"] = message["wf"];
            Context.ClientPage.ClientResponse.ShowPopup(control, "below", xmlControl);
        }

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="command">
        /// The command.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        /// <param name="workboxItem">
        /// The workbox item.
        /// </param>
        private void CreateCommand(IWorkflow workflow, WorkflowCommand command, Item item, XmlControl workboxItem)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(command, "command");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(workboxItem, "workboxItem");
            XmlControl xmlControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
            Assert.IsNotNull(xmlControl, "workboxCommand is null");
            xmlControl["Header"] = command.DisplayName;
            xmlControl["Icon"] = command.Icon;
            CommandBuilder commandBuilder = new CommandBuilder("workflow:send");
            commandBuilder.Add("id", item.ID.ToString());
            commandBuilder.Add("la", item.Language.Name);
            commandBuilder.Add("vs", item.Version.ToString());
            commandBuilder.Add("command", command.CommandID);
            commandBuilder.Add("wf", workflow.WorkflowID);
            commandBuilder.Add("ui", command.HasUI);
            commandBuilder.Add("suppresscomment", command.SuppressComment);
            xmlControl["Command"] = commandBuilder.ToString();
            workboxItem.AddControl(xmlControl);
        }

        /// <summary>
        /// Creates the item.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        /// <param name="control">
        /// The control.
        /// </param>
        private void CreateItem(IWorkflow workflow, Item item, System.Web.UI.Control control)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(control, "control");
            XmlControl xmlControl = Resource.GetWebControl("WorkboxItem") as XmlControl;
            Assert.IsNotNull(xmlControl, "workboxItem is null");
            control.Controls.Add(xmlControl);
            StringBuilder stringBuilder = new StringBuilder(" - (");
            Language language = item.Language;
            stringBuilder.Append(language.CultureInfo.DisplayName);
            stringBuilder.Append(", ");
            stringBuilder.Append(Translate.Text("version"));
            stringBuilder.Append(' ');
            stringBuilder.Append(item.Version.ToString());
            stringBuilder.Append(")");
            Assert.IsNotNull(xmlControl, "workboxItem");
            WorkflowEvent[] history = workflow.GetHistory(item);
            xmlControl["Header"] = item.GetUIDisplayName();
            xmlControl["Details"] = stringBuilder.ToString();
            xmlControl["Icon"] = item.Appearance.Icon;
            xmlControl["ShortDescription"] = (Settings.ContentEditor.RenderItemHelpAsHtml ? WebUtil.RemoveAllScripts(item.Help.ToolTip) : HttpUtility.HtmlEncode(item.Help.ToolTip));
            xmlControl["History"] = GetHistory(workflow, history);
            xmlControl["LastComments"] = HttpUtility.HtmlEncode(GetLastComments(history, item));
            xmlControl["HistoryMoreID"] = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl");
            xmlControl["HistoryClick"] = "workflow:showhistory(id=" + item.ID + ",la=" + item.Language.Name + ",vs=" + item.Version + ",wf=" + workflow.WorkflowID + ")";
            xmlControl["PreviewClick"] = "Preview(\"" + item.ID + "\", \"" + item.Language + "\", \"" + item.Version + "\")";
            xmlControl["Click"] = "Open(\"" + item.ID + "\", \"" + item.Language + "\", \"" + item.Version + "\")";
            xmlControl["DiffClick"] = "Diff(\"" + item.ID + "\", \"" + item.Language + "\", \"" + item.Version + "\")";
            xmlControl["Display"] = "none";
            string uniqueID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID(string.Empty);
            xmlControl["CheckID"] = "check_" + uniqueID;
            xmlControl["HiddenID"] = "hidden_" + uniqueID;
            xmlControl["CheckValue"] = item.ID + "," + item.Language + "," + item.Version;
            WorkflowCommand[] array = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item);
            foreach (WorkflowCommand command in array)
            {
                CreateCommand(workflow, command, item, xmlControl);
            }
        }

        /// <summary>
        /// Creates the navigator.
        /// </summary>
        /// <param name="section">The section.</param>
        /// <param name="id">The id.</param>
        /// <param name="count">The count.</param>
        /// <param name="offset">The offset.</param>
        private void CreateNavigator(Section section, string id, int count, int offset)
        {
            Assert.ArgumentNotNull(section, "section");
            Assert.ArgumentNotNull(id, "id");
            Navigator navigator = new Navigator();
            section.Controls.Add(navigator);
            navigator.ID = id;
            navigator.Offset = offset;
            navigator.Count = count;
            navigator.PageSize = PageSize;
        }

        /// <summary>
        /// Gets the history.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="events">
        /// The workflow history for the item
        /// </param>
        /// <returns>
        /// The get history.
        /// </returns>
        private string GetHistory(IWorkflow workflow, WorkflowEvent[] events)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(events, "events");
            if (events.Length <= 0)
            {
                return Translate.Text("No changes have been made.");
            }
            WorkflowEvent workflowEvent = events[events.Length - 1];
            string text = workflowEvent.User;
            string name = Context.Domain.Name;
            if (text.StartsWith(name + "\\", StringComparison.OrdinalIgnoreCase))
            {
                text = StringUtil.Mid(text, name.Length + 1);
            }
            text = StringUtil.GetString(text, Translate.Text("Unknown"));
            string stateName = GetStateName(workflow, workflowEvent.OldState);
            string stateName2 = GetStateName(workflow, workflowEvent.NewState);
            return string.Format(Translate.Text("{0} changed from <b>{1}</b> to <b>{2}</b> on {3}."), text, stateName, stateName2, DateUtil.FormatDateTime(DateUtil.ToServerTime(workflowEvent.Date), "D", Context.User.Profile.Culture));
        }

        /// <summary>
        /// Get the comments from the latest workflow event
        /// </summary>
        /// <param name="events">The workflow events to process</param>
        /// <param name="item">The item to get the comment for</param>
        /// <returns>The last comments</returns>
        private string GetLastComments(WorkflowEvent[] events, Item item)
        {
            Assert.ArgumentNotNull(events, "events");
            if (events.Length > 0)
            {
                WorkflowEvent workflowEvent = events[events.Length - 1];
                return GetWorkflowCommentsDisplayPipeline.Run(workflowEvent, item);
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="state">
        /// The state.
        /// </param>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <returns>
        /// Array of item DataUri.
        /// </returns>
        protected virtual DataUri[] GetItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.Required(Context.ContentDatabase, "Context.ContentDatabase");
            DataUri[] items = workflow.GetItems(state.StateID);
            if (items == null || items.Length == 0)
            {
                return new DataUri[0];
            }
            ArrayList arrayList = new ArrayList(items.Length);
            DataUri[] array = items;
            foreach (DataUri dataUri in array)
            {
                Item item = Context.ContentDatabase.GetItem(dataUri);
                if (item != null && item.Access.CanRead() && item.Access.CanReadLanguage() && item.Access.CanWriteLanguage() && (Context.IsAdministrator || item.Locking.CanLock() || item.Locking.HasLock()))
                {
                    arrayList.Add(dataUri);
                }
            }
            return arrayList.ToArray(typeof(DataUri)) as DataUri[];
        }

        /// <summary>
        /// Gets the items in the workflow state.
        /// </summary>
        /// <param name="state">The state to get the items for.</param>
        /// <param name="workflow">The workflow the state belongs to.</param>
        /// <returns>The items for the state.</returns>
        private StateItems GetStateItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            List<Item> list = new List<Item>();
            List<string> list2 = new List<string>();
            DataUri[] items = workflow.GetItems(state.StateID);
            bool flag = items.Length > Settings.Workbox.StateCommandFilteringItemThreshold;
            if (items != null)
            {
                DataUri[] array = items;
                foreach (DataUri uri in array)
                {
                    Item item = Context.ContentDatabase.GetItem(uri);
                    if (item != null && item.Access.CanRead() && item.Access.CanReadLanguage() && item.Access.CanWriteLanguage() && (Context.IsAdministrator || item.Locking.CanLock() || item.Locking.HasLock()))
                    {
                        list.Add(item);
                        if (!flag)
                        {
                            WorkflowCommand[] array2 = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item);
                            WorkflowCommand[] array3 = array2;
                            foreach (WorkflowCommand workflowCommand in array3)
                            {
                                if (!list2.Contains(workflowCommand.CommandID))
                                {
                                    list2.Add(workflowCommand.CommandID);
                                }
                            }
                        }
                    }
                }
            }
            if (flag)
            {
                WorkflowCommand[] source = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(state.StateID));
                list2.AddRange(from x in source
                               select x.CommandID);
            }
            StateItems stateItems = new StateItems();
            stateItems.Items = list;
            stateItems.CommandIds = list2;
            return stateItems;
        }

        /// <summary>
        /// Gets the pane ID.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <returns>
        /// The get pane id.
        /// </returns>
        private string GetPaneID(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            return "P" + Regex.Replace(workflow.WorkflowID, "\\W", string.Empty);
        }

        /// <summary>
        /// Gets the name of the state.
        /// </summary>
        /// <param name="workflow">
        /// The workflow.
        /// </param>
        /// <param name="stateID">
        /// The state ID.
        /// </param>
        /// <returns>
        /// The get state name.
        /// </returns>
        private string GetStateName(IWorkflow workflow, string stateID)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(stateID, "stateID");
            if (stateNames == null)
            {
                stateNames = new NameValueCollection();
                WorkflowState[] states = workflow.GetStates();
                foreach (WorkflowState workflowState in states)
                {
                    stateNames.Add(workflowState.StateID, workflowState.DisplayName);
                }
            }
            return StringUtil.GetString(stateNames[stateID], "?");
        }

        /// <summary>
        /// Jumps the specified sender.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        private void Jump(object sender, Message message, int offset)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(message, "message");
            string control = Context.ClientPage.ClientRequest.Control;
            string text = ShortID.Decode(control.Substring(0, 32));
            string text2 = ShortID.Decode(control.Substring(33, 32));
            control = control.Substring(0, 65);
            Offset[text2] = offset;
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            Assert.IsNotNull(workflowProvider, "Workflow provider for database \"" + Context.ContentDatabase.Name + "\" not found.");
            IWorkflow workflow = workflowProvider.GetWorkflow(text);
            Error.Assert(workflow != null, "Workflow \"" + text + "\" not found.");
            Assert.IsNotNull(workflow, "workflow");
            WorkflowState state = workflow.GetState(text2);
            Assert.IsNotNull(state, "Workflow state \"" + text2 + "\" not found.");
            Border border = new Border();
            border.ID = control + "_content";
            Border control2 = border;
            StateItems stateItems = GetStateItems(state, workflow);
            DisplayState(workflow, state, stateItems ?? new StateItems(), control2, offset, PageSize);
            Context.ClientPage.ClientResponse.SetOuterHtml(control + "_content", control2);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void Send(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    Item item = Context.ContentDatabase.Items[message["id"], Language.Parse(message["la"]), Sitecore.Data.Version.Parse(message["vs"])];
                    if (item != null)
                    {
                        InitializeCommentDialog(new List<ItemUri>
                    {
                        item.Uri
                    }, message);
                    }
                }
            }
        }

        /// <summary>
        /// Sends all.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void SendAll(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            List<ItemUri> list = new List<ItemUri>();
            string workflowID = message["wf"];
            string stateID = message["ws"];
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    WorkflowState state = workflow.GetState(stateID);
                    DataUri[] items = GetItems(state, workflow);
                    Assert.IsNotNull(items, "uris is null");
                    if (items.Length == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else
                    {
                        list = (from du in items
                                select new ItemUri(du.ItemID, du.Language, du.Version, Context.ContentDatabase)).ToList();
                        if (Settings.Workbox.WorkBoxSingleCommentForBulkOperation)
                        {
                            InitializeCommentDialog(list, message);
                        }
                        else
                        {
                            ExecutCommand(list, workflow, null, message["command"], message["ws"]);
                            Refresh();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Workflow callback to refresh the UI.
        /// </summary>
        /// <param name="args">The args for the workflow execution.</param>
        [UsedImplicitly]
        private void WorkflowCompleteRefresh(WorkflowPipelineArgs args)
        {
            Refresh();
        }

        /// <summary>
        /// Sends the selected.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void SendSelected(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            List<ItemUri> list = new List<ItemUri>();
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    foreach (string key in Context.ClientPage.ClientRequest.Form.Keys)
                    {
                        if (key != null && key.StartsWith("check_", StringComparison.InvariantCulture))
                        {
                            string name = "hidden_" + key.Substring(6);
                            string text2 = Context.ClientPage.ClientRequest.Form[name];
                            string[] array = text2.Split(',');
                            if (array.Length == 3)
                            {
                                ItemUri item = new ItemUri(array[0] ?? string.Empty, Language.Parse(array[1]), Sitecore.Data.Version.Parse(array[2]), Context.ContentDatabase);
                                list.Add(item);
                            }
                        }
                    }
                    if (list.Count == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else if (Settings.Workbox.WorkBoxSingleCommentForBulkOperation)
                    {
                        InitializeCommentDialog(list, message);
                    }
                    else
                    {
                        ExecutCommand(list, workflow, null, message["command"], message["ws"]);
                        Refresh();
                    }
                }
            }
        }

        /// <summary>
        /// Displays a single comment dialog box for multiple selected items.
        /// </summary>
        /// <param name="itemUris">A list of ItemUris</param>
        /// <param name="message">A sheer message value</param>
        private void InitializeCommentDialog(List<ItemUri> itemUris, Message message)
        {
            Context.ClientPage.ServerProperties["items"] = itemUris;
            Context.ClientPage.ServerProperties["command"] = message["command"];
            Context.ClientPage.ServerProperties["workflowid"] = message["wf"];
            Context.ClientPage.ServerProperties["workflowStateid"] = message["ws"];
            Context.ClientPage.Start(this, "Comment", new NameValueCollection
        {
            {
                "ui",
                message["ui"]
            },
            {
                "suppresscomment",
                message["suppresscomment"]
            }
        });
        }

        /// <summary>
        /// Displays comment dialog for the selected items
        /// </summary>
        /// <param name="itemUris">A List of ItemUris</param>
        /// <param name="commandId">The Command Id</param>
        /// <param name="args">Pipeline Args</param>
        protected virtual void DisplayCommentDialog(List<ItemUri> itemUris, ID commandId, ClientPipelineArgs args)
        {
            WorkflowUIHelper.DisplayCommentDialog(itemUris, commandId);
            args.WaitForPostBack();
        }

        /// <summary>
        /// Executes specific command on multiple selected items
        /// </summary>
        /// <param name="itemUris">A list of ItemUris</param>
        /// <param name="workflow">The workflow</param>
        /// <param name="fields">Fileds dictionary</param>
        /// <param name="command">The command</param>
        /// <param name="workflowStateId">The Workflow State ID</param>
        protected virtual void ExecutCommand(List<ItemUri> itemUris, IWorkflow workflow, Sitecore.Collections.StringDictionary fields, string command, string workflowStateId)
        {
            bool flag = false;
            if (fields == null)
            {
                fields = new Sitecore.Collections.StringDictionary();
            }
            foreach (ItemUri itemUri in itemUris)
            {
                Item item = Context.ContentDatabase.GetItem(itemUri.ItemID);
                if (item == null)
                {
                    flag = true;
                }
                else
                {
                    WorkflowState state = workflow.GetState(item);
                    if (state != null && (string.IsNullOrWhiteSpace(workflowStateId) || state.StateID == workflowStateId))
                    {
                        if (fields.Count < 1 || !fields.ContainsKey("Comments"))
                        {
                            string value = string.IsNullOrWhiteSpace(state.DisplayName) ? string.Empty : state.DisplayName;
                            fields.Add("Comments", value);
                        }
                        try
                        {
                            if (itemUris.Count == 1)
                            {
                                Processor completionCallback = new Processor("Workflow complete state item count", this, "WorkflowCompleteStateItemCount");
                                workflow.Execute(command, item, fields, true, completionCallback);
                            }
                            else
                            {
                                workflow.Execute(command, item, fields, true);
                            }
                        }
                        catch (WorkflowStateMissingException)
                        {
                            flag = true;
                        }
                    }
                }
            }
            if (flag)
            {
                SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.");
            }
        }

        /// <summary>
        /// Updates the ribbon.
        /// </summary>
        private void UpdateRibbon()
        {
            Ribbon ribbon = new Ribbon();
            ribbon.ID = "WorkboxRibbon";
            ribbon.CommandContext = new CommandContext();
            Ribbon ribbon2 = ribbon;
            Item item = Context.Database.GetItem("/sitecore/content/Applications/Workbox/Ribbon");
            Error.AssertItemFound(item, "/sitecore/content/Applications/Workbox/Ribbon");
            ribbon2.CommandContext.RibbonSourceUri = item.Uri;
            ribbon2.CommandContext.CustomData = IsReload;
            RibbonPanel.Controls.Add(ribbon2);
        }

        /// <summary>
        /// Wires the up navigators.
        /// </summary>
        /// <param name="control">
        /// The control.
        /// </param>
        private void WireUpNavigators(System.Web.UI.Control control)
        {
            foreach (System.Web.UI.Control control2 in control.Controls)
            {
                Navigator navigator = control2 as Navigator;
                if (navigator != null)
                {
                    navigator.Jump += Jump;
                    navigator.Previous += Jump;
                    navigator.Next += Jump;
                }
                WireUpNavigators(control2);
            }
        }

        /// <summary>
        /// Get the Workflow Provider
        /// </summary>
        /// <returns></returns>
        protected virtual IWorkflow GetWorkflowFromPage()
        {
            return Context.ContentDatabase.WorkflowProvider?.GetWorkflow(Context.ClientPage.ServerProperties["WorkflowID"] as string);
        }

        /// <summary>
        /// Workflow completion callback to refresh the counts of items in workflow states.
        /// </summary>
        /// <param name="args">The arguments for the workflow execution.</param>
        [UsedImplicitly]
        private void WorkflowCompleteStateItemCount(WorkflowPipelineArgs args)
        {
            IWorkflow workflowFromPage = GetWorkflowFromPage();
            if (workflowFromPage != null)
            {
                int itemCount = workflowFromPage.GetItemCount(args.PreviousState.StateID);
                if (PageSize > 0 && itemCount % PageSize == 0)
                {
                    if (itemCount / PageSize > 1)
                    {
                        Offset[args.PreviousState.StateID] = Offset[args.PreviousState.StateID] - 1;
                    }
                    else
                    {
                        Offset[args.PreviousState.StateID] = 0;
                    }
                }
                Dictionary<string, string> urlArguments = workflowFromPage.GetStates().ToDictionary((WorkflowState state) => state.StateID, (WorkflowState state) => Offset[state.StateID].ToString());
                Refresh(urlArguments);
            }
        }
    }
}
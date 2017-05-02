﻿using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;

using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components.Alpaca;
using Satrabel.OpenContent.Components.AppDefinitions;
using Satrabel.OpenContent.Components.Datasource;
using Satrabel.OpenContent.Components.Dnn;
using Satrabel.OpenContent.Components.Handlebars;
using Satrabel.OpenContent.Components.Indexing;
using Satrabel.OpenContent.Components.Json;
using Satrabel.OpenContent.Components.Logging;
using Satrabel.OpenContent.Components.Manifest;
using Satrabel.OpenContent.Components.Razor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Web.UI;
using Satrabel.OpenContent.Components.Localization;
using IDataSource = Satrabel.OpenContent.Components.Datasource.IDataSource;
using SecurityAccessLevel = Satrabel.OpenContent.Components.AppDefinitions.SecurityAccessLevel;


namespace Satrabel.OpenContent.Components.Render
{
    public class RenderEngine
    {
        private readonly RenderInfo _renderinfo;
        private readonly OpenContentModuleInfo _module; // active module (not datasource module)
        private readonly OpenContentSettings _settings;

        public RenderEngine(ModuleInfo viewmodule, IDictionary moduleSettings = null)
        {
            _module = new OpenContentModuleInfo(viewmodule, moduleSettings);
            _renderinfo = new RenderInfo(_module.Settings.Template, _module.Settings.IsOtherModule);
            _settings = _module.Settings;
        }

        public RenderInfo Info => _renderinfo;

        public OpenContentSettings Settings => _settings;

        public string ItemId // For detail view
        {
            private get { return _renderinfo.DetailItemId; }
            set { _renderinfo.DetailItemId = value; }
        }

        public NameValueCollection QueryString { get; set; } // Only for filtering
        public string ResourceFile { get; set; } // Only for Dnn Razor helpers

        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }
        public string MetaOther { get; set; }
        public IRenderCanvas RenderCanvas { get; set; }

        public void Render(Page page)
        {
            //start rendering           
            if (_module.Settings.Template != null)
            {
                if (!_module.Settings.Template.DataNeeded())
                {
                    // template without schema & options
                    // render the template with no data
                    _renderinfo.SetData(null, new JObject(), _module.Settings.Data);
                    _renderinfo.Files = _renderinfo.Template.Main;
                    _renderinfo.OutputString = GenerateOutputSingle(page, _renderinfo.Template.MainTemplateUri(), _renderinfo.DataJson, _renderinfo.SettingsJson, _renderinfo.Template.Main);
                }
                else if (_renderinfo.Template.IsListTemplate)
                {

                    // Multi items template
                    if (string.IsNullOrEmpty(ItemId))
                    {
                        // List template
                        if (_renderinfo.Template.Main != null)
                        {
                            // for list templates a main template need to be defined
                            _renderinfo.Files = _renderinfo.Template.Main;
                            string templateKey = GetDataList(_renderinfo, _module.Settings, _renderinfo.Template.ClientSideData);
                            if (!string.IsNullOrEmpty(templateKey) && _renderinfo.Template.Views != null && _renderinfo.Template.Views.ContainsKey(templateKey))
                            {
                                _renderinfo.Files = _renderinfo.Template.Views[templateKey];
                            }
                            if (!_renderinfo.ShowInitControl)
                            {
                                _renderinfo.OutputString = GenerateListOutput(page, _module.Settings.Template, _renderinfo.Files, _renderinfo.DataList, _renderinfo.SettingsJson);
                            }
                        }
                    }
                    else
                    {
                        LogContext.Log(_module.ViewModule.ModuleID, "RequestContext", "QueryParam Id", ItemId);
                        // detail template
                        if (_renderinfo.Template.Detail != null)
                        {
                            GetDetailData(_renderinfo, _module);
                        }
                        if (_renderinfo.Template.Detail != null && !_renderinfo.ShowInitControl)
                        {
                            _renderinfo.Files = _renderinfo.Template.Detail;
                            _renderinfo.OutputString = GenerateOutputDetail(page, _module.Settings.Template, _renderinfo.Template.Detail, _renderinfo.DataJson, _renderinfo.SettingsJson);
                        }
                        else // if itemid not corresponding to this module or no DetailTemplate present, show list template
                        {
                            // List template
                            if (_renderinfo.Template.Main != null)
                            {
                                // for list templates a main template need to be defined
                                _renderinfo.Files = _renderinfo.Template.Main;
                                string templateKey = GetDataList(_renderinfo, _settings, _renderinfo.Template.ClientSideData);
                                if (!string.IsNullOrEmpty(templateKey) && _renderinfo.Template.Views != null && _renderinfo.Template.Views.ContainsKey(templateKey))
                                {
                                    _renderinfo.Files = _renderinfo.Template.Views[templateKey];
                                }
                                if (!_renderinfo.ShowInitControl)
                                {
                                    _renderinfo.OutputString = GenerateListOutput(page, _settings.Template, _renderinfo.Files, _renderinfo.DataList, _renderinfo.SettingsJson);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // single item template
                    GetSingleData(_renderinfo, _settings);
                    if (!_renderinfo.ShowInitControl)
                    {
                        _renderinfo.OutputString = GenerateOutputSingle(page, _renderinfo.Template.MainTemplateUri(), _renderinfo.DataJson, _renderinfo.SettingsJson, _renderinfo.Template.Main);
                    }
                }
            }
        }

        public void RenderDemoData(Page page)
        {
            TemplateManifest template = _renderinfo.Template;
            if (template != null && template.IsListTemplate)
            {
                // Multi items template
                if (string.IsNullOrEmpty(_renderinfo.DetailItemId))
                {
                    // List template
                    if (template.Main != null)
                    {
                        // for list templates a main template need to be defined
                        _renderinfo.Files = _renderinfo.Template.Main;
                        /*
                        GetDataList(_renderinfo, _viewmodule.Settings, template.ClientSideData);
                        if (!_renderinfo.SettingsMissing)
                        {
                            _renderinfo.OutputString = GenerateListOutput(_renderinfo.Template.Uri().UrlFolder, template.Main, _renderinfo.DataList, _renderinfo.SettingsJson);
                        }
                         */
                    }
                }
            }
            else
            {
                bool demoExist = GetDemoData(_renderinfo, _settings);
                bool settingsNeeded = _renderinfo.Template.SettingsNeeded();

                if (demoExist && _renderinfo.DataExist && (!settingsNeeded || !string.IsNullOrEmpty(_renderinfo.SettingsJson)))
                {
                    _renderinfo.OutputString = GenerateOutputSingle(page, _renderinfo.Template.MainTemplateUri(), _renderinfo.DataJson, _renderinfo.SettingsJson, _renderinfo.Template.Main);
                }
                //too many rendering issues 
                //bool dsDataExist = _datasource.GetOtherModuleDemoData(_info, _info, _viewmodule.Settings);
                //if (dsDataExist)
                //    _info.OutputString = GenerateOutput(_info.Template.Uri(), _info.DataJson, _info.SettingsJson, null);
            }
        }

        public void IncludeResourses(Page page, Control control)
        {
            IncludeResourses(page, _renderinfo.Template);
            if (_renderinfo.Template != null && _renderinfo.Template.ClientSideData)
            {
                DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxScriptSupport();
                DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
            }
            if (_renderinfo.Files?.PartialTemplates != null)
            {
                foreach (var item in _renderinfo.Files.PartialTemplates.Where(p => p.Value.ClientSide))
                {
                    var f = new FileUri(_renderinfo.Template.ManifestFolderUri.FolderPath, item.Value.Template);
                    string s = File.ReadAllText(f.PhysicalFilePath);
                    var litPartial = new LiteralControl(s);
                    control.Controls.Add(litPartial);
                }
            }
        }

        private static void IncludeResourses(Page page, TemplateManifest template)
        {
            if (template != null)
            {
                var cssfilename = new FileUri(Path.ChangeExtension(template.MainTemplateUri().FilePath, "css"));
                if (cssfilename.FileExists)
                {
                    App.Services.ClientResourceManager.RegisterStyleSheet(page, cssfilename.UrlFilePath);
                }
                var jsfilename = new FileUri(Path.ChangeExtension(template.MainTemplateUri().FilePath, "js"));
                if (jsfilename.FileExists)
                {
                    App.Services.ClientResourceManager.RegisterScript(page, jsfilename.UrlFilePath, 100);
                }
                App.Services.ClientResourceManager.RegisterScript(page, "~/DesktopModules/OpenContent/js/opencontent.js");
            }
        }

        public void IncludeMeta(Page page)
        {
            if (!string.IsNullOrEmpty(MetaTitle))
            {
                page.Title = MetaTitle;
            }
            if (!string.IsNullOrEmpty(MetaDescription))
            {
                PageUtils.SetPageDescription(page, MetaDescription);
            }
            if (!string.IsNullOrEmpty(MetaOther))
            {
                PageUtils.SetPageMeta(page, MetaOther);
            }
        }

        #region Data

        private string GetDataList(RenderInfo info, OpenContentSettings settings, bool clientSide)
        {
            string templateKey = "";
            info.ResetData();

            IDataSource ds = DataSourceManager.GetDataSource(_settings.Manifest.DataSource);
            var dsContext = OpenContentUtils.CreateDataContext(_module);

            IEnumerable<IDataItem> resultList = new List<IDataItem>();
            if (clientSide || !info.Files.DataInTemplate)
            {
                if (ds.Any(dsContext))
                {
                    info.SetData(resultList, settings.Data);
                    info.DataExist = true;
                }

                if (info.Template.Views != null)
                {
                    var indexConfig = OpenContentUtils.GetIndexConfig(info.Template);
                    templateKey = GetTemplateKey(indexConfig);
                }
            }
            else
            {
                //server side
                bool useLucene = info.Template.Manifest.Index;
                if (useLucene)
                {
                    PortalSettings portalSettings = PortalSettings.Current;
                    var indexConfig = OpenContentUtils.GetIndexConfig(info.Template);
                    if (info.Template.Views != null)
                    {
                        templateKey = GetTemplateKey(indexConfig);
                    }
                    bool isEditable = _module.ViewModule.CheckIfEditable(portalSettings);
                    QueryBuilder queryBuilder = new QueryBuilder(indexConfig);
                    queryBuilder.Build(settings.Query, !isEditable, portalSettings.UserId, DnnLanguageUtils.GetCurrentCultureCode(), portalSettings.UserInfo.Social.Roles, QueryString);

                    resultList = ds.GetAll(dsContext, queryBuilder.Select).Items;
                    if (LogContext.IsLogActive)
                    {
                        //LogContext.Log(_module.ModuleID, "RequestContext", "EditMode", !addWorkFlow);
                        LogContext.Log(_module.ViewModule.ModuleID, "RequestContext", "IsEditable", isEditable);
                        LogContext.Log(_module.ViewModule.ModuleID, "RequestContext", "UserRoles", portalSettings.UserInfo.Social.Roles.Select(r => r.RoleName));
                        LogContext.Log(_module.ViewModule.ModuleID, "RequestContext", "CurrentUserId", portalSettings.UserId);
                        var logKey = "Query";
                        LogContext.Log(_module.ViewModule.ModuleID, logKey, "select", queryBuilder.Select);
                        //LogContext.Log(_module.ModuleID, logKey, "result", resultList);
                    }
                    //App.Services.Logger.Debug($"Query returned [{0}] results.", total);
                    if (!resultList.Any())
                    {
                        //App.Services.Logger.Debug($"Query did not return any results. API request: [{0}], Lucene Filter: [{1}], Lucene Query:[{2}]", settings.Query, queryDef.Filter == null ? "" : queryDef.Filter.ToString(), queryDef.Query == null ? "" : queryDef.Query.ToString());
                        if (ds.Any(dsContext))
                        {
                            info.SetData(resultList, settings.Data);
                            info.DataExist = true;
                        }
                    }
                }
                else
                {
                    resultList = ds.GetAll(dsContext, null).Items;
                    //if (LogContext.IsLogActive)
                    //{
                    //    var logKey = "Get all data of module";
                    //    LogContext.Log(_module.ModuleID, logKey, "result", resultList);
                    //}
                }
                if (resultList.Any())
                {
                    info.SetData(resultList, settings.Data);
                }
            }
            return templateKey;
        }

        private void GetDetailData(RenderInfo info, OpenContentModuleInfo module)
        {
            info.ResetData();
            var ds = DataSourceManager.GetDataSource(module.Settings.Manifest.DataSource);
            var dsContext = OpenContentUtils.CreateDataContext(module);

            var dsItem = ds.Get(dsContext, info.DetailItemId);
            //if (LogContext.IsLogActive)
            //{
            //    var logKey = "Get detail data";
            //    LogContext.Log(_module.ModuleID, logKey, "debuginfo", dsItems.DebugInfo);
            //}

            if (dsItem != null)
            {
                //check permissions
                var portalSettings = PortalSettings.Current;
                bool isEditable = _module.ViewModule.CheckIfEditable(portalSettings);
                if (!isEditable)
                {
                    var indexConfig = OpenContentUtils.GetIndexConfig(info.Template);
                    string raison;
                    if (!OpenContentUtils.HaveViewPermissions(dsItem, portalSettings.UserInfo, indexConfig, out raison))
                    {
                        if (module.ViewModule.HasEditRightsOnModule())
                            throw new NotAuthorizedException(404, $"No detail view permissions for id={info.DetailItemId}  (due to {raison}) \nGo into Edit Mode to view/change the item");
                        else
                            throw new NotAuthorizedException(404, $"Access denied. You might want to contact your administrator for more information. (due to {raison})");
                    }
                }
                info.SetData(dsItem, dsItem.Data, module.Settings.Data);
            }
        }

        private void GetSingleData(RenderInfo info, OpenContentSettings settings)
        {
            info.ResetData();

            IDataSource ds = DataSourceManager.GetDataSource(_settings.Manifest.DataSource);
            var dsContext = OpenContentUtils.CreateDataContext(_module, -1, true);

            var dsItem = ds.Get(dsContext, null);
            if (dsItem != null)
            {
                info.SetData(dsItem, dsItem.Data, settings.Data);
            }
        }

        public bool GetDemoData(RenderInfo info, OpenContentSettings settings)
        {
            info.ResetData();
            //bool settingsNeeded = false;
            FileUri dataFilename = null;
            if (info.Template != null)
            {
                dataFilename = new FileUri(info.Template.ManifestFolderUri.UrlFolder, "data.json"); ;
            }
            if (dataFilename != null && dataFilename.FileExists)
            {
                string fileContent = File.ReadAllText(dataFilename.PhysicalFilePath);
                string settingContent = "";
                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    if (settings.Template != null && info.Template.MainTemplateUri().FilePath == settings.Template.MainTemplateUri().FilePath)
                    {
                        settingContent = settings.Data;
                    }
                    if (string.IsNullOrEmpty(settingContent))
                    {
                        var settingsFilename = info.Template.MainTemplateUri().PhysicalFullDirectory + "\\" + info.Template.Key.ShortKey + "-data.json";
                        if (File.Exists(settingsFilename))
                        {
                            settingContent = File.ReadAllText(settingsFilename);
                        }
                        else
                        {
                            //string schemaFilename = info.Template.Uri().PhysicalFullDirectory + "\\" + info.Template.Key.ShortKey + "-schema.json";
                            //settingsNeeded = File.Exists(schemaFilename);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(fileContent))
                    info.SetData(null, fileContent, settingContent);
            }
            return !info.ShowInitControl; //!string.IsNullOrWhiteSpace(info.DataJson) && (!string.IsNullOrWhiteSpace(info.SettingsJson) || !settingsNeeded);
        }
        private string GetTemplateKey(FieldConfig IndexConfig)
        {
            string templateKey = "";
            if (QueryString != null)
            {
                foreach (string key in QueryString)
                {
                    if (IndexConfig?.Fields != null && IndexConfig.Fields.Any(f => f.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var indexConfig = IndexConfig.Fields.Single(f => f.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
                        string val = QueryString[key];
                        if (string.IsNullOrEmpty(templateKey))
                            templateKey = key;
                        else
                            templateKey += "-" + key;
                    }
                }
            }
            return templateKey;
        }

        #endregion

        #region ExecuteTemplates
        private string ExecuteRazor(FileUri template, dynamic model)
        {
            string webConfig = template.PhysicalFullDirectory;
            webConfig = webConfig.Remove(webConfig.LastIndexOf("\\")) + "\\web.config";
            if (!File.Exists(webConfig))
            {
                string filename = HostingEnvironment.MapPath("~/DesktopModules/OpenContent/Templates/web.config");
                File.Copy(filename, webConfig);
            }
            var writer = new StringWriter();
            try
            {
                var razorEngine = new RazorEngine("~/" + template.FilePath, RenderCanvas, ResourceFile);
                razorEngine.Render(writer, model);
            }
            catch (Exception ex)
            {
                LoggingUtils.RenderEngineException(this, ex);
                string stack = string.Join("\n", ex.StackTrace.Split('\n').Where(s => s.Contains("\\Portals\\") && s.Contains("in")).Select(s => s.Substring(s.IndexOf("in"))).ToArray());
                throw new TemplateException("Failed to render Razor template " + template.FilePath + "\n" + stack, ex, model, template.FilePath);
            }
            return writer.ToString();
        }
        private string ExecuteTemplate(Page page, TemplateManifest templateManifest, TemplateFiles files, FileUri templateUri, object model)
        {
            var templateVirtualFolder = templateManifest.ManifestFolderUri.UrlFolder;
            string output;
            Stopwatch stopwatch = null;
            if (LogContext.IsLogActive)
            {
                var logKey = "Render template";
                LogContext.Log(_module.ViewModule.ModuleID, logKey, "template", templateUri.FilePath);
                LogContext.Log(_module.ViewModule.ModuleID, logKey, "model", model);
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }
            if (templateUri.Extension != ".hbs")
            {
                output = ExecuteRazor(templateUri, model);
            }
            else
            {
                HandlebarsEngine hbEngine = new HandlebarsEngine();
                output = hbEngine.Execute(page, files, templateVirtualFolder, model);
            }
            if (stopwatch != null)
            {
                stopwatch.Stop();
                var logKey = "Render template";
                LogContext.Log(_module.ViewModule.ModuleID, logKey, "render time (ms)", stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
            }
            return output;
        }

        #endregion

        #region Generate output

        private string GenerateOutputDetail(Page page, TemplateManifest templateManifest, TemplateFiles files, JToken dataJson, string settingsJson)
        {
            // detail template
            var templateVirtualFolder = templateManifest.ManifestFolderUri.UrlFolder;
            if (!string.IsNullOrEmpty(files.Template))
            {
                string physicalTemplateFolder = HostingEnvironment.MapPath(templateVirtualFolder);
                FileUri templateUri = CheckFiles(templateManifest, files);

                if (dataJson != null)
                {
                    var mf = new ModelFactorySingle(_renderinfo.Data, settingsJson, physicalTemplateFolder, _renderinfo.Template.Manifest, _renderinfo.Template, files, _module, PortalSettings.Current);
                    mf.Detail = true;
                    object model;
                    if (templateUri.Extension != ".hbs") // razor
                    {
                        model = mf.GetModelAsDynamic();
                    }
                    else // handlebars
                    {
                        if (App.Services.GlobalSettings().GetFastHandlebars())
                            model = mf.GetModelAsDictionary();
                        else
                            model = mf.GetModelAsDynamic();

                    }
                    if (!string.IsNullOrEmpty(_renderinfo.Template.Manifest.DetailMetaTitle))
                    {
                        HandlebarsEngine hbEngine = new HandlebarsEngine();
                        //page.Title
                        MetaTitle = hbEngine.Execute(_renderinfo.Template.Manifest.DetailMetaTitle, model);
                    }
                    if (!string.IsNullOrEmpty(_renderinfo.Template.Manifest.DetailMetaDescription))
                    {
                        HandlebarsEngine hbEngine = new HandlebarsEngine();
                        //PageUtils.SetPageDescription(page, hbEngine.Execute(_renderinfo.Template.Manifest.DetailMetaDescription, model));
                        MetaDescription = hbEngine.Execute(_renderinfo.Template.Manifest.DetailMetaDescription, model);
                    }
                    if (!string.IsNullOrEmpty(_renderinfo.Template.Manifest.DetailMeta))
                    {
                        HandlebarsEngine hbEngine = new HandlebarsEngine();
                        //PageUtils.SetPageMeta(page, hbEngine.Execute(_renderinfo.Template.Manifest.DetailMeta, model));
                        MetaOther = hbEngine.Execute(_renderinfo.Template.Manifest.DetailMeta, model);
                    }
                    return ExecuteTemplate(page, templateManifest, files, templateUri, model);
                }
                else
                {
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

        private string GenerateOutputSingle(Page page, FileUri template, JToken dataJson, string settingsJson, TemplateFiles files)
        {
            if (template != null)
            {
                string templateVirtualFolder = template.UrlFolder;
                string physicalTemplateFolder = HostingEnvironment.MapPath(templateVirtualFolder);
                if (dataJson != null)
                {
                    ModelFactorySingle mf;

                    if (_renderinfo.Data == null)
                    {
                        // demo data
                        mf = new ModelFactorySingle(_renderinfo.DataJson, settingsJson, physicalTemplateFolder, _renderinfo.Template.Manifest, _renderinfo.Template, files, _module, PortalSettings.Current);
                    }
                    else
                    {
                        mf = new ModelFactorySingle(_renderinfo.Data, settingsJson, physicalTemplateFolder, _renderinfo.Template.Manifest, _renderinfo.Template, files, _module, PortalSettings.Current);
                    }
                    if (template.Extension != ".hbs") // razor
                    {
                        dynamic model = mf.GetModelAsDynamic();
                        if (LogContext.IsLogActive)
                        {
                            var logKey = "Render single item template";
                            LogContext.Log(_module.ViewModule.ModuleID, logKey, "template", template.FilePath);
                            LogContext.Log(_module.ViewModule.ModuleID, logKey, "model", model);
                        }
                        return ExecuteRazor(template, model);
                    }
                    else // handlebars
                    {
                        object model;
                        if (App.Services.GlobalSettings().GetFastHandlebars())
                            model = mf.GetModelAsDictionary();
                        else
                            model = mf.GetModelAsDynamic();
                        if (LogContext.IsLogActive)
                        {
                            var logKey = "Render single item template";
                            LogContext.Log(_module.ViewModule.ModuleID, logKey, "template", template.FilePath);
                            LogContext.Log(_module.ViewModule.ModuleID, logKey, "model", model);
                        }
                        HandlebarsEngine hbEngine = new HandlebarsEngine();
                        return hbEngine.Execute(page, template, model);
                    }
                }
                else
                {
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

        private string GenerateListOutput(Page page, TemplateManifest templateManifest, TemplateFiles files, IEnumerable<IDataItem> dataList, string settingsJson)
        {
            var templateVirtualFolder = templateManifest.ManifestFolderUri.UrlFolder;
            if (!string.IsNullOrEmpty(files.Template))
            {
                string physicalTemplateFolder = HostingEnvironment.MapPath(templateVirtualFolder);
                FileUri templateUri = CheckFiles(templateManifest, files);
                if (dataList != null)
                {
                    ModelFactoryMultiple mf = new ModelFactoryMultiple(dataList, settingsJson, physicalTemplateFolder, _renderinfo.Template.Manifest, _renderinfo.Template, files, _module, PortalSettings.Current);
                    object model;
                    if (templateUri.Extension != ".hbs") // razor
                    {
                        model = mf.GetModelAsDynamic();
                    }
                    else // handlebars
                    {
                        if (App.Services.GlobalSettings().GetFastHandlebars())
                            model = mf.GetModelAsDictionary();
                        else
                            model = mf.GetModelAsDynamic();
                    }
                    return ExecuteTemplate(page, templateManifest, files, templateUri, model);
                }
            }
            return "";
        }

        private static FileUri CheckFiles(TemplateManifest templateManifest, TemplateFiles files)
        {
            if (files == null)
            {
                throw new Exception("Manifest.json missing or incomplete");
            }
            var templateUri = new FileUri(templateManifest.ManifestFolderUri, files.Template);
            if (!templateUri.FileExists)
            {
                throw new Exception("Template " + templateUri.UrlFilePath + " don't exist");
            }
            if (files.PartialTemplates != null)
            {
                foreach (var partial in files.PartialTemplates)
                {
                    var partialTemplateUri = new FileUri(templateManifest.ManifestFolderUri, partial.Value.Template);
                    if (!partialTemplateUri.FileExists)
                        throw new Exception("PartialTemplate " + partialTemplateUri.UrlFilePath + " don't exist");
                }
            }
            return templateUri;
        }
        #endregion

        public List<MenuAction> GetMenuActions()
        {
            var actions = new List<MenuAction>();

            TemplateManifest template = _settings.Template;
            bool templateDefined = template != null;
            bool listMode = template != null && template.IsListTemplate;

            bool isListPageRequest = listMode && string.IsNullOrEmpty(_renderinfo.DetailItemId);
            bool isDetailPageRequest = listMode && !string.IsNullOrEmpty(_renderinfo.DetailItemId);

            //Add item / Edit Item
            if (templateDefined && template.DataNeeded() && !_settings.Manifest.DisableEdit)
            {
                string title = Localizer.Instance.GetString(isListPageRequest ? "Add.Action" : "Edit.Action", ResourceFile);
                if (!string.IsNullOrEmpty(_settings.Manifest.Title))
                {
                    title = title + " " + _settings.Manifest.Title;
                }

                actions.Add(
                    new MenuAction(
                        title,
                        isListPageRequest ? "~/DesktopModules/OpenContent/images/addcontent2.png" : "~/DesktopModules/OpenContent/images/editcontent2.png",
                        isDetailPageRequest ? RenderCanvas.EditUrl("id", _renderinfo.DetailItemId) : RenderCanvas.EditUrl(),
                        ActionType.Add
                        )
                    );
            }

            //Add AdditionalData manage actions
            if (templateDefined && template.Manifest.AdditionalDataDefined() && !_settings.Manifest.DisableEdit)
            {
                foreach (var addData in template.Manifest.AdditionalDataDefinition)
                {
                    if (addData.Value.SourceRelatedDataSource == RelatedDataSourceType.AdditionalData)
                    {
                        actions.Add(
                            new MenuAction(
                                addData.Value.Title,
                                "~/DesktopModules/OpenContent/images/editcontent2.png",
                                RenderCanvas.EditUrl("key", addData.Key, "EditAddData"),
                                ActionType.Edit
                            )
                        );
                    }
                    else
                    {
                        actions.Add(
                            new MenuAction(
                                addData.Value.Title,
                                "~/DesktopModules/OpenContent/images/editcontent2.png",
                                DnnUrlUtils.NavigateUrl(addData.Value.DataTabId),
                                ActionType.Edit
                            )
                        );
                    }
                }
            }

            //Manage Form Submissions
            if (templateDefined && OpenContentUtils.FormExist(_settings.Template.ManifestFolderUri))
            {
                actions.Add(
                    new MenuAction(
                        "Submissions",
                        "~/DesktopModules/OpenContent/images/editcontent2.png",
                        RenderCanvas.EditUrl("Submissions"),
                        ActionType.Edit
                    )
                );
            }

            //Edit Template Settings
            if (templateDefined && _settings.Template.SettingsNeeded())
            {
                actions.Add(
                    new MenuAction(
                        Localizer.Instance.GetString("EditSettings.Action", ResourceFile),
                        "~/DesktopModules/OpenContent/images/editsettings2.png",
                        RenderCanvas.EditUrl("EditSettings"),
                        ActionType.Misc,
                        SecurityAccessLevel.AdminRights
                    )
                );
            }

            //Edit Form Settings
            if (templateDefined && OpenContentUtils.FormExist(_settings.Template.ManifestFolderUri))
            {
                actions.Add(
                    new MenuAction(
                        Localizer.Instance.GetString("FormSettings.Action", ResourceFile),
                        "~/DesktopModules/OpenContent/images/editsettings2.png",
                        RenderCanvas.EditUrl("formsettings"),
                        ActionType.Misc,
                        SecurityAccessLevel.AdminRights
                    )
                );
            }

            //Switch Template
            actions.Add(
                new MenuAction(
                Localizer.Instance.GetString("EditInit.Action", ResourceFile),
                "~/DesktopModules/OpenContent/images/editinit.png",
                RenderCanvas.EditUrl("EditInit"),
                ActionType.Misc,
                SecurityAccessLevel.AdminRights
                )
            );

            //Edit Filter Settings
            if (templateDefined && listMode)
            {
                if (_settings.Manifest.Index)
                {
                    actions.Add(
                        new MenuAction(
                            Localizer.Instance.GetString("EditQuery.Action", ResourceFile),
                            "~/DesktopModules/OpenContent/images/editfilter.png",
                            RenderCanvas.EditUrl("EditQuery"),
                            ActionType.Misc,
                            SecurityAccessLevel.AdminRights
                        )
                    );
                }
            }

            //Form Builder
            if (templateDefined && OpenContentUtils.BuildersExist(_settings.Template.ManifestFolderUri))
                actions.Add(
                    new MenuAction(
                        Localizer.Instance.GetString("Builder.Action", ResourceFile),
                        "~/DesktopModules/OpenContent/images/formbuilder.png",
                        RenderCanvas.EditUrl("FormBuilder"),
                        ActionType.Misc,
                        SecurityAccessLevel.AdminRights
                    )
                );

            //Edit Template Files
            if (templateDefined)
                actions.Add(
                    new MenuAction(
                        Localizer.Instance.GetString("EditTemplate.Action", ResourceFile),
                        "~/DesktopModules/OpenContent/images/edittemplate.png",
                        RenderCanvas.EditUrl("EditTemplate"),
                        ActionType.Misc,
                        SecurityAccessLevel.SuperUserRights
                    )
                );

            //Edit Raw Data
            if (templateDefined && _settings.Manifest != null &&
               (template.DataNeeded() || template.SettingsNeeded() || template.Manifest.AdditionalDataDefined()) && !_settings.Manifest.DisableEdit)
            {
                actions.Add(
                    new MenuAction(
                        Localizer.Instance.GetString("EditData.Action", ResourceFile),
                        "~/DesktopModules/OpenContent/images/edit.png",
                        isDetailPageRequest ? RenderCanvas.EditUrl("id", _renderinfo.DetailItemId, "EditData") : RenderCanvas.EditUrl("EditData"),
                        ActionType.Edit,
                        SecurityAccessLevel.SuperUserRights
                    )
                );
            }

            //Template Exchange
            actions.Add(
                new MenuAction(
                    Localizer.Instance.GetString("ShareTemplate.Action", ResourceFile),
                    "~/DesktopModules/OpenContent/images/exchange.png",
                    RenderCanvas.EditUrl("ShareTemplate"),
                    ActionType.Misc,
                    SecurityAccessLevel.SuperUserRights
                )
            );

            //Edit Global Settings
            actions.Add(
                new MenuAction(
                    Localizer.Instance.GetString("EditGlobalSettings.Action", ResourceFile),
                    "~/DesktopModules/OpenContent/images/settings.png",
                    RenderCanvas.EditUrl("EditGlobalSettings"),
                    ActionType.Misc,
                    SecurityAccessLevel.SuperUserRights
                )
            );

            //Help
            actions.Add(
                new MenuAction(
                    Localizer.Instance.GetString("Help.Action", ResourceFile),
                    "~/DesktopModules/OpenContent/images/help.png",
                    "https://opencontent.readme.io",
                    ActionType.Misc,
                    SecurityAccessLevel.SuperUserRights,
                    true
                )
            );

            return actions;
        }

    }

 
}
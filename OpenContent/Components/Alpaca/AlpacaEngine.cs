﻿using DotNetNuke.Web.Client;
using DotNetNuke.Web.Client.ClientResourceManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Hosting;
using System.Web.UI;
using DotNetNuke.UI.Modules;
using System.Web.UI.WebControls;
using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components.Json;
using System.Web.UI.HtmlControls;
using DotNetNuke.Entities.Portals;


namespace Satrabel.OpenContent.Components.Alpaca
{
    public class AlpacaEngine
    {
        private string _virtualDirectory;

        private string VirtualDirectory
        {
            get
            {
                return _virtualDirectory;
            }
            set
            {
                _virtualDirectory = value.TrimStart('\\');
            }
        }

        private string Prefix { get; set; }

        public Page Page { get; private set; }
        public ModuleInstanceContext ModuleContext { get; private set; }

        public AlpacaEngine(Page Page, ModuleInstanceContext moduleContext, string virtualDir, string filePrefix)
        {
            this.Page = Page;
            this.ModuleContext = moduleContext;
            VirtualDirectory = virtualDir;
            Prefix = filePrefix;
        }

        public void RegisterAll(bool bootstrap = false, bool loadBootstrap = false)
        {
            RegisterAlpaca(bootstrap, loadBootstrap);
            RegisterTemplates();
            RegisterScripts(bootstrap);
            RegisterFields(bootstrap);
        }
        public void RegisterAll(bool bootstrap) // for openform
        {
            RegisterAll(bootstrap, false);
        }

        private void RegisterAlpaca(bool bootstrap, bool loadBootstrap)
        {
            if (loadBootstrap)
            {
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/bootstrap/js/bootstrap.min.js", FileOrder.Js.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/bootstrap/css/bootstrap.min.css", FileOrder.Css.DefaultPriority);
            }
            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/lib/handlebars/handlebars.js", FileOrder.Js.DefaultPriority);
            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/lib/typeahead.js/dist/typeahead.bundle.min.js", FileOrder.Js.DefaultPriority);

            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/wysihtml/wysihtml-toolbar.js", FileOrder.Js.DefaultPriority + 1);
            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/wysihtml/parser_rules/advanced_opencontent.js", FileOrder.Js.DefaultPriority + 1);
            if (bootstrap)
            {
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/alpaca/bootstrap/alpaca.css", FileOrder.Css.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/alpaca/css/alpaca-dnnbootstrap.css", FileOrder.Css.DefaultPriority);
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/alpaca/bootstrap/alpaca.js", FileOrder.Js.DefaultPriority + 1);
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/alpaca/js/views/dnnbootstrap.js", FileOrder.Js.DefaultPriority + 2);
            }
            else
            {
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/alpaca/css/alpaca-dnn.css", FileOrder.Css.DefaultPriority);
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/alpaca/web/alpaca.js", FileOrder.Js.DefaultPriority + 1);
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/alpaca/js/views/dnn.js", FileOrder.Js.DefaultPriority + 2);
            }
            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/alpaca/js/fields/dnn/dnnfields.js", FileOrder.Js.DefaultPriority + 3);

            ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/alpacaengine.js", FileOrder.Js.DefaultPriority + 10);

            ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/css/font-awesome/css/font-awesome.min.css", FileOrder.Css.DefaultPriority + 1);
        }
        public void RegisterTemplates()
        {

            var body = (HtmlGenericControl)Page.FindControl("Body");
            if (body.FindControl("oc-dnntemplates") == null)
            {
                string templates = File.ReadAllText(HostingEnvironment.MapPath("~/DesktopModules/OpenContent/alpaca/templates/dnn-edit/dnntemplates.html"));
                var lit = new LiteralControl(templates);
                lit.ID = "oc-dnntemplates";

                body.Controls.Add(lit);
            }

        }

        public void RegisterScripts(bool bootstrap)
        {
            ServicesFramework.Instance.RequestAjaxScriptSupport();
            ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
            if (!bootstrap)
            {
                JavaScript.RequestRegistration(CommonJs.DnnPlugins); // dnnPanels
            }
            JavaScript.RequestRegistration(CommonJs.jQueryFileUpload); // image file upload
            DotNetNuke.UI.Utilities.ClientAPI.RegisterClientVariable(Page, "oc_websiteRoot", FileUri.NormalizedApplicationPath, true);
            JavaScript.RequestRegistration(CommonJs.DnnPlugins); // avoid js error TypeError: $(...).jScrollPane is not a function
        }

        private void RegisterFields(bool bootstrap)
        {
            bool allFields = string.IsNullOrEmpty(VirtualDirectory);
            List<string> fieldTypes = new List<string>();
            if (!allFields)
            {
                JToken options = GetOptions();
                if (options != null)
                {
                    fieldTypes = FieldTypes(options);
                }
            }
            if (allFields || fieldTypes.Contains("address"))
            {
                string apikey = OpenContentControllerFactory.Instance.OpenContentGlobalSettingsController.GetGoogleApiKey();
                ClientResourceManager.RegisterScript(Page, "//maps.googleapis.com/maps/api/js?v=3.exp&libraries=places"+(string.IsNullOrEmpty(apikey) ? "":"&key="+apikey), FileOrder.Js.DefaultPriority);
            }
            if (allFields || fieldTypes.Contains("imagecropper") || fieldTypes.Contains("imagecrop") || fieldTypes.Contains("imagecrop2"))
            {
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/cropper/cropper.js", FileOrder.Js.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/cropper/cropper.css", FileOrder.Css.DefaultPriority);
            }
            if (allFields ||
                    fieldTypes.Contains("select2") || fieldTypes.Contains("image2") || fieldTypes.Contains("file2") || fieldTypes.Contains("url2") ||
                    fieldTypes.Contains("mlimage2") || fieldTypes.Contains("mlfile2") || fieldTypes.Contains("mlurl2") || fieldTypes.Contains("mlfolder2") ||
                    fieldTypes.Contains("imagecrop2")
                )
            {
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/select2/select2.js", FileOrder.Js.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/select2/select2.css", FileOrder.Css.DefaultPriority);
                if (bootstrap)
                {
                    ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/select2/select2-bootstrap.min.css", FileOrder.Css.DefaultPriority + 1);
                }
            }

            //<!-- bootstrap datetimepicker for date, time and datetime controls -->
            //<dnncl:DnnJsInclude ID="DnnJsInclude13" runat="server" FilePath="~/DesktopModules/OpenContent/js/lib/moment/min/moment-with-locales.min.js" Priority="106" ForceProvider="DnnPageHeaderProvider" />
            //<dnncl:DnnJsInclude ID="DnnJsInclude14" runat="server" FilePath="~/DesktopModules/OpenContent/js/lib/eonasdan-bootstrap-datetimepicker/build/js/bootstrap-datetimepicker.min.js" Priority="106" ForceProvider="DnnPageHeaderProvider" />
            //<dnncl:DnnCssInclude ID="DnnCssInclude2" runat="server" FilePath="~/DesktopModules/OpenContent/js/lib/eonasdan-bootstrap-datetimepicker/build/css/bootstrap-datetimepicker.css" AddTag="false" />

            if (allFields || fieldTypes.Contains("date") || fieldTypes.Contains("datetime") || fieldTypes.Contains("time"))
            {
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/lib/moment/min/moment-with-locales.min.js", FileOrder.Js.DefaultPriority, "DnnPageHeaderProvider");
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/lib/eonasdan-bootstrap-datetimepicker/build/js/bootstrap-datetimepicker.min.js", FileOrder.Js.DefaultPriority + 1, "DnnPageHeaderProvider");
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/lib/eonasdan-bootstrap-datetimepicker/build/css/bootstrap-datetimepicker.css", FileOrder.Css.DefaultPriority);
            }
            if (allFields || fieldTypes.Contains("ckeditor") || fieldTypes.Contains("mlckeditor"))
            {
                var form = Page.FindControl("Form");
                if (form.FindControl("CKDNNporid") == null)
                {
                    if (CKEditorIngoThaWatchaIsInstalled())
                    {
                        ClientResourceManager.RegisterScript(Page, "~/Providers/HtmlEditorProviders/CKEditor/ckeditor.js", FileOrder.Js.DefaultPriority);
                        DotNetNuke.UI.Utilities.ClientAPI.RegisterClientVariable(Page, "PortalId", ModuleContext.PortalId.ToString(), true);
                        var CKDNNporid = new HiddenField();
                        CKDNNporid.ID = "CKDNNporid";
                        CKDNNporid.ClientIDMode = ClientIDMode.Static;

                        form.Controls.Add(CKDNNporid);
                        CKDNNporid.Value = ModuleContext.PortalId.ToString();
                        GenerateEditorLoadScript(ModuleContext.PortalId);
                    }
                    else if (CKEditorDnnConnectIsInstalled())
                    {
                        ClientResourceManager.RegisterScript(Page, "~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.5.3/ckeditor.js", FileOrder.Js.DefaultPriority);
                        DotNetNuke.UI.Utilities.ClientAPI.RegisterClientVariable(Page, "PortalId", ModuleContext.PortalId.ToString(), true);
                        var CKDNNporid = new HiddenField();
                        CKDNNporid.ID = "CKDNNporid";
                        CKDNNporid.ClientIDMode = ClientIDMode.Static;

                        form.Controls.Add(CKDNNporid);
                        CKDNNporid.Value = ModuleContext.PortalId.ToString();
                        GenerateEditorLoadScript(ModuleContext.PortalId);
                    }
                    else
                    {
                        Log.Logger.Warn("Failed to load CKEeditor. Please install a DNN CKEditor Provider.");
                    }
                }
            }
            if (allFields || fieldTypes.Contains("icon"))
            {
                ClientResourceManager.RegisterScript(Page, "~/DesktopModules/OpenContent/js/fontIconPicker/jquery.fonticonpicker.min.js", FileOrder.Js.DefaultPriority, "DnnPageHeaderProvider");
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/fontIconPicker/css/jquery.fonticonpicker.min.css", FileOrder.Css.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/js/fontIconPicker/themes/grey-theme/jquery.fonticonpicker.grey.min.css", FileOrder.Css.DefaultPriority);
                ClientResourceManager.RegisterStyleSheet(Page, "~/DesktopModules/OpenContent/css/glyphicons/glyphicons.css", FileOrder.Css.DefaultPriority + 1);
            }
        }

        private bool CKEditorDnnConnectIsInstalled()
        {
            return File.Exists(HostingEnvironment.MapPath("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.5.3/ckeditor.js"));
        }

        private bool CKEditorIngoThaWatchaIsInstalled()
        {
            return File.Exists(HostingEnvironment.MapPath("~/Providers/HtmlEditorProviders/CKEditor/ckeditor.js"));
        }

        private JToken GetOptions()
        {
            string physicalDirectory = HostingEnvironment.MapPath("~/" + VirtualDirectory);
            JToken optionsJson = null;
            // default options
            string optionsFilename = physicalDirectory + "\\" + (string.IsNullOrEmpty(Prefix) ? "" : Prefix + "-") + "options.json";
            if (File.Exists(optionsFilename))
            {
                string fileContent = File.ReadAllText(optionsFilename);
                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    optionsJson = JObject.Parse(fileContent);
                }
            }
            // language options
            optionsFilename = physicalDirectory + "\\" + (string.IsNullOrEmpty(Prefix) ? "" : Prefix + "-") + "options." + DnnLanguageUtils.GetCurrentCultureCode() + ".json";
            if (File.Exists(optionsFilename))
            {
                string fileContent = File.ReadAllText(optionsFilename);
                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    if (optionsJson == null)
                        optionsJson = JObject.Parse(fileContent);
                    else
                        optionsJson = optionsJson.JsonMerge(JObject.Parse(fileContent));
                }
            }
            return optionsJson;
        }

        private List<string> FieldTypes(JToken options)
        {
            List<string> types = new List<string>();
            var fields = options["fields"];
            if (fields != null)
            {
                foreach (JProperty fieldProp in fields.Children())
                {
                    var field = fieldProp.First();
                    var fieldtype = field["type"];
                    if (fieldtype != null)
                    {
                        types.Add(fieldtype.ToString());
                    }
                    var subtypes = FieldTypes(field);
                    types.AddRange(subtypes);
                }
            }
            else if (options["items"] != null)
            {
                if (options["items"]["type"] != null)
                {
                    var fieldtype = options["items"]["type"] as JValue;
                    types.Add(fieldtype.Value.ToString());
                }
                var subtypes = FieldTypes(options["items"]);
                types.AddRange(subtypes);
            }
            return types;
        }

        public static string AlpacaCulture(string CultureCode)
        {
            string[] alpacaLocales = { "zh_CN", "hr_HR", "fr_FR", "de_AT", "it_IT", "ja_JP", "pl_PL", "pt_BR", "es_ES" };
            string lang = CultureCode.Replace("-", "_");
            foreach (var item in alpacaLocales)
            {
                if (item == lang)
                {
                    return lang;
                }
            }
            string lang2 = lang.Substring(0, 2);
            foreach (var item in alpacaLocales)
            {
                if (item.Substring(0, 2) == lang2)
                {
                    return lang;
                }
            }
            return "en_US";
        }

        /// <summary>
        /// Generates the editor load script.
        /// </summary>
        private void GenerateEditorLoadScript(int portalId)
        {
            //var editorVar = string.Format(
            //    "editor{0}",
            //    ClientID.Substring(ClientID.LastIndexOf("_", StringComparison.Ordinal) + 1).Replace(
            //        "-", string.Empty));

            //var editorFixedId = ClientID.Replace("-", string.Empty).Replace(".", string.Empty);
            var editorFixedId = "opencontentCK";

            ////if (HasMsAjax)
            ////{
            //var postBackScript =
            //    string.Format(
            //        @"if (CKEDITOR && CKEDITOR.instances && CKEDITOR.instances.{0}) {{ CKEDITOR.instances.{0}.updateElement(); CKEDITOR.instances.{0}.destroy(); }}",
            //        editorFixedId);

            //RegisterOnSubmitStatement(
            //    GetType(), string.Format("CKEditor_OnAjaxSubmit_{0}", editorFixedId), postBackScript);
            ////}

            var editorScript = new StringBuilder();

            //editorScript.AppendFormat(
            //    "Sys.WebForms.PageRequestManager.getInstance().add_pageLoaded(LoadCKEditorInstance_{0});", editorFixedId);

            //editorScript.AppendFormat("function LoadCKEditorInstance_{0}(sender,args) {{", editorFixedId);

            //editorScript.AppendFormat(
            //    @"if (jQuery(""[id*='UpdatePanel']"").length == 0 && CKEDITOR && CKEDITOR.instances && CKEDITOR.instances.{0}) {{ CKEDITOR.instances.{0}.updateElement();}}",
            //    editorFixedId);

            //editorScript.AppendFormat(
            //    "if (document.getElementById('{0}') == null){{return;}}",
            //    editorFixedId);

            //// Render EditorConfig
            //var editorConfigScript = new StringBuilder();
            //editorConfigScript.AppendFormat("var editorConfig{0} = {{", editorVar);

            //var keysCount = Settings.Keys.Count;
            //var currentCount = 0;

            //// Write options
            //foreach (string key in Settings.Keys)
            //{
            //    var value = Settings[key];

            //    currentCount++;

            //    // Is boolean state or string
            //    if (value.Equals("true", StringComparison.InvariantCultureIgnoreCase)
            //        || value.Equals("false", StringComparison.InvariantCultureIgnoreCase) || value.StartsWith("[")
            //        || value.StartsWith("{") || Utility.IsNumeric(value))
            //    {
            //        if (value.Equals("True"))
            //        {
            //            value = "true";
            //        }
            //        else if (value.Equals("False"))
            //        {
            //            value = "false";
            //        }

            //        editorConfigScript.AppendFormat("{0}:{1}", key, value);

            //        editorConfigScript.Append(currentCount == keysCount ? "};" : ",");
            //    }
            //    else
            //    {
            //        if (key == "browser")
            //        {
            //            continue;
            //        }

            //        editorConfigScript.AppendFormat("{0}:\'{1}\'", key, value);

            //        editorConfigScript.Append(currentCount == keysCount ? "};" : ",");
            //    }
            //}

            //editorScript.AppendFormat(
            //    "if (CKEDITOR.instances.{0}){{return;}}",
            //    editorFixedId);

            //// Check if we can use jQuery or $, and if both fail use ckeditor without the adapter
            //editorScript.Append("if (jQuery().ckeditor) {");

            //editorScript.AppendFormat("var {0} = jQuery('#{1}').ckeditor(editorConfig{0});", editorVar, editorFixedId);

            //editorScript.Append("} else if ($.ckeditor) {");

            //editorScript.AppendFormat("var {0} = $('#{1}').ckeditor(editorConfig{0});", editorVar, editorFixedId);

            //editorScript.Append("} else {");

            //editorScript.AppendFormat("var {0} = CKEDITOR.replace( '{1}', editorConfig{0});", editorVar, editorFixedId);

            //editorScript.Append("}");

            //// firefox maximize fix
            //editorScript.Append("CKEDITOR.on('instanceReady', function (ev) {");
            //editorScript.Append("ev.editor.on('maximize', function () {");
            //editorScript.Append("if (ev.editor.commands.maximize.state == 1) {");
            //editorScript.Append("var mainDocument = CKEDITOR.document;");
            //editorScript.Append("CKEDITOR.env.gecko && mainDocument.getDocumentElement().setStyle( 'position', 'fixed' );");
            //editorScript.Append("}");
            //editorScript.Append("});");
            //editorScript.Append("});");

            editorScript.Append("if(CKEDITOR && CKEDITOR.config){");
            editorScript.Append("  CKEDITOR.config.portalId = " + portalId + ";");
            editorScript.Append("  CKEDITOR.config.enableConfigHelper = " + EnableConfigHelper() + ";");
            editorScript.Append("};");

            //if(CKEDITOR && CKEDITOR.config && CKEDITOR.config.plugins ))

            // End of LoadScript
            //editorScript.Append("}");

            //ClientResourceManager.RegisterScript(string.Format(@"{0}_CKE_Config", editorFixedId), editorConfigScript.ToString(), true);
            RegisterStartupScript(string.Format(@"{0}_CKE_Startup", editorFixedId), editorScript.ToString(), true);
        }

        private string EnableConfigHelper()
        {
            if (File.Exists(HostingEnvironment.MapPath("~/Providers/HtmlEditorProviders/CKEditor/plugins/confighelper/plugin.js")))
            {
                return "true";
            }
            else if (File.Exists(HostingEnvironment.MapPath("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.5.3/plugins/confighelper/plugin.js")))
            {
                return "true";
            }
            return "false";
        }

        private void RegisterStartupScript(string key, string script, bool addScriptTags)
        {
            ScriptManager.RegisterStartupScript(Page, GetType(), key, script, addScriptTags);
        }

    }
}
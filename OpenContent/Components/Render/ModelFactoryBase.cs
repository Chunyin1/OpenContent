﻿using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Localization;
using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components.Datasource;
using Satrabel.OpenContent.Components.Dnn;
using Satrabel.OpenContent.Components.Handlebars;
using Satrabel.OpenContent.Components.Json;
using Satrabel.OpenContent.Components.Manifest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Satrabel.OpenContent.Components.Render
{
    public abstract class ModelFactoryBase
    {
        private readonly string _settingsJson;
        private readonly string _physicalTemplateFolder;
        protected readonly TemplateFiles _templateFiles;
        protected readonly int _portalId;
        private readonly string _cultureCode;
        // only multiple
        protected readonly Manifest.Manifest _manifest;
        protected readonly TemplateManifest _templateManifest;
        protected readonly PortalSettings _portalSettings;
        protected readonly OpenContentModuleInfo _module;
        protected readonly int _detailTabId;

        public ModelFactoryBase(string settingsJson, string physicalTemplateFolder, Manifest.Manifest manifest, TemplateManifest templateManifest, TemplateFiles templateFiles, OpenContentModuleInfo module, PortalSettings portalSettings)

        {
            //this._dataJson = dataJson;
            this._settingsJson = settingsJson;
            this._physicalTemplateFolder = physicalTemplateFolder;
            this._manifest = manifest;
            this._templateFiles = templateFiles;
            this._module = module;
            this._portalSettings = portalSettings;
            this._portalId = portalSettings.PortalId;
            this._templateManifest = templateManifest;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
        }

        public ModelFactoryBase(string settingsJson, string physicalTemplateFolder, Manifest.Manifest manifest, TemplateManifest templateManifest, TemplateFiles templateFiles, OpenContentModuleInfo module, int portalId, string cultureCode)

        {
            //this._dataJson = dataJson;
            this._settingsJson = settingsJson;
            this._physicalTemplateFolder = physicalTemplateFolder;
            this._manifest = manifest;
            this._templateFiles = templateFiles;
            this._module = module;
            this._cultureCode = cultureCode;
            this._portalId = portalId;
            this._templateManifest = templateManifest;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
        }

        public ModelFactoryBase(OpenContentModuleInfo module, PortalSettings portalSettings)
        {
            OpenContentSettings settings = module.Settings;
            this._settingsJson = settings.Data;
            this._physicalTemplateFolder = settings.Template.ManifestFolderUri.PhysicalFullDirectory + "\\";
            this._manifest = settings.Template.Manifest;
            this._templateFiles = settings.Template != null ? settings.Template.Main : null;
            this._module = module;
            this._portalSettings = portalSettings;
            this._portalId = portalSettings.PortalId;
            this._templateManifest = settings.Template;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
        }

        public JObject Options { get; set; } // alpaca options.json format


        public dynamic GetModelAsDynamic(bool onlyData = false)
        {
            if (_portalSettings == null) onlyData = true;

            JToken model = GetModelAsJson(onlyData);
            return JsonUtils.JsonToDynamic(model.ToString());
        }

        /*
        public JToken GetModelAsJson(bool onlyData = false)
        {
            if (_portalSettings == null) onlyData = true;

            if (_dataList == null)
            {
                return GetModelAsJsonFromJson(onlyData);
            }
            else
            {
                return GetModelAsJsonFromList(onlyData);
            }
        }
        */
        public abstract JToken GetModelAsJson(bool onlyData = false);

        protected void EnhanceSelect2(JObject model, JObject enhancedModel)
        {
            string colName = string.IsNullOrEmpty(_templateManifest.Collection) ? "Items" : _templateManifest.Collection;
            bool enhance = (_manifest.AdditionalDataDefined() && enhancedModel["AdditionalData"] != null && enhancedModel["Options"] != null) ||
                            (_templateFiles.Model != null && _templateFiles.Model.ContainsKey(colName) && enhancedModel["Options"] != null);

            /*
            if (_manifest.AdditionalDataDefined() && enhancedModel["AdditionalData"] != null && enhancedModel["Options"] != null)
            {
                JsonUtils.LookupJson(model, enhancedModel["AdditionalData"] as JObject, enhancedModel["Options"] as JObject);
            }
            */
            if (enhance)
            {
                var colManifest = _templateFiles.Model == null ? null : _templateFiles.Model[colName];
                var includes = colManifest == null ? null : colManifest.Includes;
                var ds = DataSourceManager.GetDataSource(_manifest.DataSource);
                var dsContext = OpenContentUtils.CreateDataContext(_module);
                JsonUtils.LookupJson(model, enhancedModel["AdditionalData"] as JObject, enhancedModel["Options"] as JObject, includes, (col, id) =>
                {
                    dsContext.Collection = col;
                    var dsItem = ds.Get(dsContext, id);
                    if (dsItem != null && dsItem.Data is JObject)
                    {
                        return dsItem.Data as JObject;
                    }
                    else
                    {
                        JObject res = new JObject();
                        res["Id"] = id;
                        res["Collection"] = col;
                        res["Title"] = "unknow";
                        return res;
                    }
                });
            }
            if (enhancedModel["Options"] != null)
            {
                JsonUtils.LookupSelect2InOtherModule(model, enhancedModel["Options"] as JObject);
            }

        }

        protected void ExtendModel(JObject model, bool onlyData)
        {
            if (_portalSettings == null) onlyData = true;

            var ds = DataSourceManager.GetDataSource(_manifest.DataSource);
            var dsContext = OpenContentUtils.CreateDataContext(_module);
            if (_templateFiles != null)
            {
                bool includeSchema = !onlyData && _templateFiles.SchemaInTemplate;
                bool includeOptions = _templateFiles.OptionsInTemplate;
                if (includeSchema || includeOptions)
                {
                    var alpaca = ds.GetAlpaca(dsContext, includeSchema, includeOptions, false);
                    // include SCHEMA info in the Model
                    if (includeSchema)
                    {
                        model["Schema"] = alpaca["schema"];
                    }
                    // include OPTIONS info in the Model
                    if (includeOptions)
                    {
                        model["Options"] = alpaca["options"];
                    }
                }
                // include additional data in the Model
                if (_templateFiles.AdditionalDataInTemplate && _manifest.AdditionalDataDefined())
                {
                    var additionalData = model["AdditionalData"] = new JObject();
                    foreach (var item in _manifest.AdditionalDataDefinition)
                    {
                        var dataManifest = item.Value;
                        IDataItem dataItem = ds.GetData(dsContext, dataManifest.ScopeType, dataManifest.StorageKey ?? item.Key);
                        JToken additionalDataJson = new JObject();
                        var json = dataItem?.Data;
                        if (json != null)
                        {
                            if (LocaleController.Instance.GetLocales(_portalId).Count > 1)
                            {
                                JsonUtils.SimplifyJson(json, GetCurrentCultureCode());
                            }
                            additionalDataJson = json;
                        }
                        additionalData[(item.Value.ModelKey ?? item.Key).ToLowerInvariant()] = additionalDataJson;
                    }
                }
                // include collections
                if (_templateFiles.Model != null)
                {
                    var dsColContext = OpenContentUtils.CreateDataContext(_module);
                    var collections = model["Collections"] = new JObject();
                    foreach (var item in _templateFiles.Model.Where(c => c.Key != _templateManifest.Collection))
                    {
                        var colManifest = item.Value;
                        dsContext.Collection = item.Key;
                        IDataItems dataItems = ds.GetAll(dsColContext, null);
                        var colDataJson = new JArray();
                        foreach (var dataItem in dataItems.Items)
                        {
                            var json = dataItem.Data;
                            
                            if (json != null && LocaleController.Instance.GetLocales(_portalId).Count > 1)
                            {
                                JsonUtils.SimplifyJson(json, GetCurrentCultureCode());
                            }
                            if (json is JObject)
                            {
                                JObject context = new JObject();
                                json["Context"] = context;
                                context["Id"] = dataItem.Id;
                                EnhanceSelect2(json as JObject, model);
                            }
                            colDataJson.Add(json);
                        }
                        collections[item.Key] = colDataJson;
                    }
                }
            }
            // include settings in the Model
            if (!onlyData && _templateManifest.SettingsNeeded() && !string.IsNullOrEmpty(_settingsJson))
            {
                try
                {
                    var jsonSettings = JToken.Parse(_settingsJson);
                    if (LocaleController.Instance.GetLocales(_portalId).Count > 1)
                    {
                        JsonUtils.SimplifyJson(jsonSettings, GetCurrentCultureCode());
                    }
                    model["Settings"] = jsonSettings;
                }
                catch (Exception ex)
                {
                    throw new Exception("Error parsing Json of Settings", ex);
                }
            }

            // include static localization in the Model
            if (!onlyData)
            {
                JToken localizationJson = null;
                string localizationFilename = _physicalTemplateFolder + GetCurrentCultureCode() + ".json";
                if (File.Exists(localizationFilename))
                {
                    string fileContent = File.ReadAllText(localizationFilename);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        localizationJson = fileContent.ToJObject("Localization: " + localizationFilename);
                    }
                }
                if (localizationJson != null)
                {
                    model["Localization"] = localizationJson;
                }
            }
            if (!onlyData)
            {
                // include CONTEXT in the Model
                JObject context = new JObject();
                model["Context"] = context;
                context["ModuleId"] = _module.ViewModule.ModuleID;
                context["GoogleApiKey"] = OpenContentControllerFactory.Instance.OpenContentGlobalSettingsController.GetGoogleApiKey();
                context["ModuleTitle"] = _module.ViewModule.ModuleTitle;
                context["AddUrl"] = DnnUrlUtils.EditUrl(_module.ViewModule.ModuleID, _portalSettings);
                var editIsAllowed = !_manifest.DisableEdit && IsEditAllowed(-1);
                context["IsEditable"] = editIsAllowed; //allowed to edit the item or list (meaning allow Add)
                context["IsEditMode"] = IsEditMode;
                context["PortalId"] = _portalId;
                context["MainUrl"] = Globals.NavigateURL(_detailTabId, false, _portalSettings, "", GetCurrentCultureCode());
            }
        }

        protected bool IsEditAllowed(int createdByUser)
        {
            string editRole = _manifest.GetEditRole();
            return (IsEditMode || OpenContentUtils.HasEditRole(_portalSettings, editRole, createdByUser)) // edit Role can edit whtout be in edit mode
                    && OpenContentUtils.HasEditPermissions(_portalSettings, _module.ViewModule, editRole, createdByUser);
        }

        protected string GetCurrentCultureCode()
        {
            if (string.IsNullOrEmpty(_cultureCode))
            {
                return DnnLanguageUtils.GetCurrentCultureCode();
            }
            else
            {
                return _cultureCode;
            }
        }

        private bool? _isEditMode;

        protected bool IsEditMode
        {
            get
            {
                //Perform tri-state switch check to avoid having to perform a security
                //role lookup on every property access (instead caching the result)
                if (!_isEditMode.HasValue)
                {
                    _isEditMode = _module.DataModule.CheckIfEditable(PortalSettings.Current);
                }
                return _isEditMode.Value;
            }
        }

    }
}
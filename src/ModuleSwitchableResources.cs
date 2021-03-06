﻿using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SimpleFuelSwitch
{
    /// <summary>
    /// This class represents one "option" for a switchable fuel tank.  It can store
    /// one or more resources (e.g. "liquid fuel", or "liquid fuel and oxidizer"),
    /// and has a descriptive name suitable for UI display (e.g. "LFO"). This is
    /// what controls the UI display in the part info window on the parts pane
    /// in the editor.
    ///
    /// Typical usage is for a part to have *one* ModuleSimpleFuelSwitch, accompanied
    /// by two or more ModuleSwitchableResources. (Of course two or more. If you only
    /// had one, there would be nothing to switch to and there wouldn't be any point
    /// to this mod!)
    /// </summary>
    class ModuleSwitchableResources : PartModule, IModuleInfo
    {
        private static readonly HashSet<string> NO_LINKED_VARIANTS = new HashSet<string>();

        /// <summary>
        /// This is an ID string, not displayed to the player, which is used by the mod
        /// to identify this instance of the module. It needs to be unique within the
        /// part. Any name is OK except the reserved word "default". It's recommended to
        /// use the same resourcesId across all parts for the same list of resource types,
        /// since this mod includes per-resourcesId config for how to format and display
        /// them.
        /// </summary>
        [KSPField]
        public string resourcesId = null;

        /// <summary>
        /// This determines the title that's displayed in the UI for this set of resources
        /// (e.g. "LF" or "LFO").  Generally should be something short, in order to fit in
        /// the limited screen real estate available. It's optional; if not provided, then
        /// a display name will be generated from the display names of the resources involved
        /// (though that will be a lot longer and might not fit well on the screen).
        /// </summary>
        [KSPField]
        public string displayName;

        /// <summary>
        /// How the "pick a resource set" field is labeled in the editor UI, e.g. "Fuel Type" or whatever.
        /// </summary>
        [KSPField]
        public string selectorFieldName = LocalizeUtil.GetString("#SimpleFuelSwitch_resourceLabel");

        /// <summary>
        /// When true, indicates that this set of resources should be picked initially by default.
        /// </summary>
        [KSPField]
        public bool isDefault = false;

        /// <summary>
        /// Optional. If supplied, will "link" this resource option with the named variant, so that
        /// switching to this resource will automatically apply that variant, and vice-versa.
        /// Note that it's possible to link multiple variants to this resource selection by
        /// including multiple variant names delimited by commas. You can also specify * as the
        /// value, meaning "wildcard", if you want to link ALL variants except those that
        /// explicitly specify something else.
        /// </summary>
        [KSPField]
        public string linkedVariant = "";

        private SwitchableResource[] resources = null;
        private string info = null;
        private string primaryField = null;
        private string longTitle = null;

        /// <summary>
        /// Here when loading at KSP startup time, opening a ship in the editor, or switching to a ship in flight.
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (HighLogic.LoadedScene == GameScenes.LOADING) OnInitialGameLoading(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (resourcesId == ResourceInfoFormatter.DEFAULT_ID)
            {
                throw new Exception("Invalid resourcesId on " + part.name + " uses reserved word '" + ResourceInfoFormatter.DEFAULT_ID + "'");
            }
        }

        /// <summary>
        /// Here when the game is first loading up.
        /// </summary>
        /// <param name="node"></param>
        private void OnInitialGameLoading(ConfigNode node)
        {
            Loader.Initialize();

            // Load the resources from config.
            resources = SwitchableResource.Load(node);
            longTitle = SwitchableResource.LongTitleOf(resources);

            // If they haven't specified a resources title in the config,
            // use the auto-generated long one.
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = longTitle;
            }

            // Add the resources to the global map. This is how the ModuleSimpleFuelSwitch will
            // control behavior when the player is working with a ship in the editor.
            SwitchableResourceSet.Add(
                part.name,
                resourcesId,
                displayName,
                selectorFieldName,
                ParseLinkedVariants(linkedVariant),
                isDefault,
                resources,
                part.Resources);

            // Everything else in this class is concerned simply with setting up stuff
            // to display appropriate info in the part info window from the parts pane
            // of the vehicle editor.

            info = FormatInfo(resourcesId, resources);
            if (resources.Length == 0)
            {
                primaryField = longTitle;
            }
            else
            {
                primaryField = LocalizeUtil.Format(
                    "#SimpleFuelSwitch_primaryInfoFormat",
                    selectorFieldName,
                    displayName,
                    ResourcePrimaryInfoFormatter.Format(resourcesId, resources));
            }
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            // No custom drawing needed.
            return null;
        }

        /// <summary>
        /// This is what's shown as the bold green title of the
        /// info panel at right in the parts pane.
        /// </summary>
        /// <returns></returns>
        public string GetModuleTitle()
        {
            string titleTag = isDefault
                ? "#SimpleFuelSwitch_titleFormatDefault"
                : "#SimpleFuelSwitch_titleFormat";
            return LocalizeUtil.Format(titleTag, selectorFieldName, displayName);
        }

        /// <summary>
        /// This is the "executive summary" that gets shown in the main
        /// part of the part info window in the parts pane.
        /// </summary>
        /// <returns></returns>
        public string GetPrimaryField()
        {
            return primaryField;
        }

        /// <summary>
        /// This is what's shown in the body of the info panel
        /// at right in the parts pane.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return info;
        }

        /// <summary>
        /// Parse a comma-delimited string into a set of linked variants.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static HashSet<string> ParseLinkedVariants(string text)
        {
            if (string.IsNullOrEmpty(text)) return NO_LINKED_VARIANTS;
            string[] tokens = text.Split(',');
            HashSet<string> results = new HashSet<string>();
            for (int i = 0; i < tokens.Length; ++i)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token)) continue;
                results.Add(token);
            }
            return (results.Count > 0) ? results : NO_LINKED_VARIANTS;
        }

        /// <summary>
        /// Formats the information to display in the info pane on the right side of
        /// the part's pop-up window on the editor.
        /// </summary>
        /// <param name="resourcesId"></param>
        /// <param name="resources"></param>
        /// <returns></returns>
        private static string FormatInfo(string resourcesId, SwitchableResource[] resources)
        {
            if (resources.Length == 0) return LocalizeUtil.GetString("#SimpleFuelSwitch_noResources");

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < resources.Length; ++i)
            {
                if (i > 0) builder.Append("\n");
                builder.Append(LocalizeUtil.Format(
                    "#SimpleFuelSwitch_detailedInfoFormat",
                    resources[i].definition.displayName,
                    ResourceInfoFormatter.Format(resourcesId, resources[i])));
            }
            return builder.ToString();
        }
    }
}

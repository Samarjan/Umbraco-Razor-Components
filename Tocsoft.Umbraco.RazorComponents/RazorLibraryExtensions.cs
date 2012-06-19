﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using Tocsoft.Common.Helpers;
using Tocsoft.Common.Helpers.Web;
using umbraco.cms.businesslogic.macro;
using umbraco.MacroEngines;
using umbraco.MacroEngines.Library;

namespace Tocsoft.Umbraco.RazorComponents
{
    public static class RazorLibraryExtensions
    {
        public static string RenderMacro(int pageId, string aliasOrPath, params object[] properties)
        {
            return GetMacroControl(pageId, aliasOrPath, properties).RenderControlToString();
        }

        public static Control GetMacroControl(int pageId, string aliasOrPath, params object[] properties)
        {
            //converting object[] into a single hash table merging common property names,
            //overwriting previously inserted values.
            Hashtable attribs = new Hashtable();
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    var dic = prop.ToDictionary();

                    foreach (var d in dic)
                    {
                        if (attribs.ContainsKey(d.Key))
                            attribs[d.Key] = d.Value;
                        else
                            attribs.Add(d.Key, d.Value);
                    }
                }
            }

            umbraco.macro macro;

            //fit is starts with ~/ then we assume we are trying to render a file and not an aliased macro
            if (aliasOrPath.StartsWith("~/"))
            {
                //this code is adapted from the macro user control
                macro = new umbraco.macro();
                macro.GenerateMacroModelPropertiesFromAttributes(attribs);
                macro.Model.ScriptName = aliasOrPath;
                macro.Model.MacroType = MacroTypes.Script;
                if (attribs["Cache"].IsNotNull())
                {
                    if (attribs["Cache"] is int)
                    {
                        macro.Model.CacheDuration = (int)attribs["Cache"];
                    }
                }
            }
            else
            {
                macro = umbraco.macro.GetMacro(aliasOrPath);
            }

            return macro.renderMacro(new Hashtable(), pageId);
        }

        public static IHtmlString RenderMacro(this RazorLibraryCore ctx, string aliasOrPath, params object[] properties)
        {
            //wrap the control returned in a ControlHtmlString that is used to render the control to a
            //string then exposes that string as a HtmlString so Razor will render the contents correctly
            return new LazyHtmlString(()=> RenderMacro(ctx.Node.Id, aliasOrPath, properties));
        }

        private static string ImageUrlFromMediaItem(RazorLibraryCore ctx, int mediaId, string cropProperty, string cropName)
        {
            string url = null;
            DynamicMedia media = ctx.MediaById(mediaId);
            if (media != null)
            {
                if (media.HasProperty(cropProperty))
                {
                    dynamic d = new DynamicXml(media.GetPropertyValue(cropProperty)).Find("@name", cropName);
                    url = (string)d.url;
                }

                if (string.IsNullOrWhiteSpace(url) && media.HasValue("umbracoFile"))
                {
                    url = media.GetPropertyValue("umbracoFile");
                }
            }
            return url ?? "";
        }

        private static string ImageUrlFromXml(RazorLibraryCore ctx, DynamicXml media, string cropProperty, string cropName)
        {
            string url = null;
            if (media != null)
            {
                var crop = new DynamicXml(media.DescendantsOrSelf(x => x.Name == cropProperty).FirstOrDefault().ToXml());
                try
                {
                    if (crop != null)
                    {
                        dynamic c = crop.Find("@name", cropName);
                        url = (string)c.url;
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(url))
                {
                    var f = media.Descendants(x => x.Name == "umbracoFile").FirstOrDefault();
                    if (f != null)
                        url = f.InnerText;
                }
            }
            return url ?? "";
        }

        public static IEnumerable<string> ImageUrls(this RazorLibraryCore ctx, DynamicNode node, string alias, string cropProperty, string cropName)
        {
            var mediaProp = node.GetProperty(alias);

            if (mediaProp != null && !string.IsNullOrWhiteSpace(mediaProp.Value))
            {
                if (mediaProp.Value.Contains('<'))
                {
                    foreach (var m in new DynamicXml(mediaProp.Value).OfType<DynamicXml>())
                    {
                        var url = ImageUrlFromXml(ctx, m, cropProperty, cropName);
                        if (!url.IsNullOrWhiteSpace())
                            yield return url;
                    }
                }
                else
                {
                    //we look like a list ofr ids
                    foreach (var val in mediaProp.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int mediaId = 0;
                        if (int.TryParse(val, out mediaId))
                        {
                            var url = ImageUrlFromMediaItem(ctx, mediaId, cropProperty, cropName);
                            if (!url.IsNullOrWhiteSpace())
                                yield return url;
                        }
                    }
                    //we look like xml
                }
            }
        }

        public static string ImageUrl(this RazorLibraryCore ctx, DynamicNode node, string alias, string cropProperty, string cropName)
        {
            return ImageUrls(ctx, node, alias, cropProperty, cropName).FirstOrDefault();
        }

        public static IEnumerable<string> ImageUrls(this RazorLibraryCore ctx, string alias, string cropProperty, string cropName)
        {
            return ImageUrls(ctx, new DynamicNode(ctx.Node), alias, cropProperty, cropName);
        }

        public static string ImageUrl(this RazorLibraryCore ctx, string alias, string cropProperty, string cropName)
        {
            return ImageUrl(ctx, new DynamicNode(ctx.Node), alias, cropProperty, cropName);
        }
    }
}
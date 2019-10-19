﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ModernWpf.Controls
{
    public class RecyclePool
    {
        public void PutElement(
            UIElement element,
            string key)
        {
            PutElementCore(element, key, null /* owner */);
        }

        public void PutElement(
            UIElement element,
            string key,
            UIElement owner)
        {
            PutElementCore(element, key, owner);
        }

        public UIElement TryGetElement(
            string key)
        {
            return TryGetElementCore(key, null /* owner */);
        }

        public UIElement TryGetElement(
            string key,
            UIElement owner)
        {
            return TryGetElementCore(key, owner);
        }

        protected virtual void PutElementCore(
            UIElement element,
            string key,
            UIElement owner)
        {
            var winrtKey = key;
            var winrtOwner = owner;
            var winrtOwnerAsPanel = EnsureOwnerIsPanelOrNull(winrtOwner);

            ElementInfo elementInfo = new ElementInfo(element, winrtOwnerAsPanel);

            if (m_elements.TryGetValue(winrtKey, out var elements))
            {
                elements.Add(elementInfo);
            }
            else
            {
                List<ElementInfo> pool = new List<ElementInfo>();
                pool.Add(elementInfo);
                m_elements.Add(winrtKey, pool);
            }
        }

        protected virtual UIElement TryGetElementCore(
            string key,
            UIElement owner)
        {
            if (m_elements.TryGetValue(key, out var elements))
            {
                if (elements.Count > 0)
                {
                    ElementInfo elementInfo = new ElementInfo(null, null);
                    // Prefer an element from the same owner or with no owner so that we don't incur
                    // the enter/leave cost during recycling.
                    // TODO: prioritize elements with the same owner to those without an owner.
                    var winrtOwner = owner;
                    var index = elements.FindIndex(elemInfo => elemInfo.Owner == winrtOwner || elementInfo.Owner == null);

                    if (index >= 0)
                    {
                        elementInfo = elements[index];
                        // elements.erase(iter);
                        elements.RemoveAt(index);
                    }
                    else
                    {
                        elementInfo = elements.Last();
                        elements.RemoveAt(elements.Count - 1);
                    }

                    var ownerAsPanel = EnsureOwnerIsPanelOrNull(winrtOwner);
                    if (elementInfo.Owner != null && elementInfo.Owner != ownerAsPanel)
                    {
                        // Element is still under its parent. remove it from its parent.
                        var panel = elementInfo.Owner;
                        if (panel != null)
                        {
                            int childIndex = panel.Children.IndexOf(elementInfo.Element);
                            bool found = childIndex >= 0;
                            if (!found)
                            {
                                throw new Exception("ItemsRepeater's child not found in its Children collection.");
                            }

                            panel.Children.RemoveAt(childIndex);
                        }
                    }

                    return elementInfo.Element;
                }
            }

            return null;
        }

        #region Properties

        internal static readonly DependencyProperty ReuseKeyProperty =
            DependencyProperty.RegisterAttached(
                "ReuseKey",
                typeof(string),
                typeof(RecyclePool),
                new PropertyMetadata(string.Empty));

        internal static string GetReuseKey(UIElement element)
        {
            return (string)element.GetValue(ReuseKeyProperty);
        }

        internal static void SetReuseKey(UIElement element, string value)
        {
            element.SetValue(ReuseKeyProperty, value);
        }

        //public static readonly DependencyProperty PoolInstanceProperty =
        //    DependencyProperty.RegisterAttached(
        //        "PoolInstance",
        //        typeof(RecyclePool),
        //        typeof(RecyclePool),
        //        null);

        public static RecyclePool GetPoolInstance(DataTemplate dataTemplate)
        {
            //return dataTemplate.GetValue(s_poolInstanceProperty) as RecyclePool;
            s_templatesToPools.TryGetValue(dataTemplate, out RecyclePool value);
            return value;
        }

        public static void SetPoolInstance(DataTemplate dataTemplate, RecyclePool value)
        {
            //dataTemplate.SetValue(s_poolInstanceProperty, value);
            s_templatesToPools.Remove(dataTemplate);
            s_templatesToPools.Add(dataTemplate, value);
        }

        internal static readonly DependencyProperty OriginTemplateProperty =
            DependencyProperty.RegisterAttached(
                "OriginTemplate",
                typeof(DataTemplate),
                typeof(RecyclePool),
                null);

        #endregion

        private Panel EnsureOwnerIsPanelOrNull(UIElement owner)
        {
            Panel ownerAsPanel = null;
            if (owner != null)
            {
                ownerAsPanel = owner as Panel;
                if (ownerAsPanel == null)
                {
                    throw new Exception("owner must to be a Panel or null.");
                }
            }

            return ownerAsPanel;
        }

        private struct ElementInfo
        {
            public ElementInfo(UIElement element, Panel owner)
            {
                Element = element;
                Owner = owner;
            }

            public UIElement Element { get; }
            public Panel Owner { get; }
        }

        private readonly Dictionary<string, List<ElementInfo>> m_elements = new Dictionary<string, List<ElementInfo>>();
        private static readonly ConditionalWeakTable<DataTemplate, RecyclePool> s_templatesToPools = new ConditionalWeakTable<DataTemplate, RecyclePool>();
    }
}
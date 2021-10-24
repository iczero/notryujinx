using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;

namespace Ryujinx.Ava.Common.Locale
{
    public class LocaleExtension : MarkupExtension
    {
        public LocaleExtension(LocaleEnum key)
        {
            Key = key.ToString();
        }

        public string Key { get; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            string keyToUse = Key;

            ReflectionBindingExtension binding = new($"[{keyToUse}]")
            {
                Mode = BindingMode.OneWay, Source = LocaleManager.Instance
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
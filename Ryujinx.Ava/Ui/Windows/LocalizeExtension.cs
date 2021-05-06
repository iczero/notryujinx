using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;

namespace Ryujinx.Ava.Ui.Windows
{
    public class LocalizeExtension : MarkupExtension
    {
        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            string keyToUse = Key;

            ReflectionBindingExtension binding = new($"[{keyToUse}]")
            {
                Mode = BindingMode.OneWay, Source = Localizer.Instance
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace CncWallStation.Localization
{
    /// <summary>
    /// WPF 动态本地化 MarkupExtension。
    /// 用法：Text="{l:Loc SomeResourceKey}"
    /// 内部创建 Binding 到 LocalizationService.Instance[Key]，
    /// 当语言切换时自动刷新所有绑定目标。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        /// <summary>
        /// .resx 资源文件中的字符串键名。
        /// </summary>
        public string Key { get; set; } = string.Empty;

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            // 在设计模式下直接返回键名，避免设计器报错
            if (IsInDesignMode(serviceProvider))
                return $"[[{Key}]]";

            // 创建 Binding 到 LocalizationService.Instance 的索引器
            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // 如果是用于 DependencyProperty，返回 BindingExpression
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
                && target.TargetObject is DependencyObject
                && target.TargetProperty is DependencyProperty)
            {
                return binding.ProvideValue(serviceProvider);
            }

            // 用于非依赖属性（如 Freezable），直接求值
            return LocalizationService.Instance[Key];
        }

        private static bool IsInDesignMode(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                return target.TargetObject is DependencyObject depObj
                       && System.ComponentModel.DesignerProperties.GetIsInDesignMode(depObj);
            }
            return false;
        }
    }
}

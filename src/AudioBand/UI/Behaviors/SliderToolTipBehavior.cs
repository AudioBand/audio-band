using AudioBand.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace AudioBand.UI
{
    /// <summary>
    /// Show time tooltip behavior for a slider.
    /// </summary>
    internal class SliderToolTipBehavior : Behavior<Slider>
    {
        /// <summary>
        /// Dependency property for <see cref="PrefixProperty"/>.
        /// </summary>
        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register("Prefix", typeof(string), typeof(SliderToolTipBehavior), new PropertyMetadata(default(string)));

        /// <summary>
        /// Dependency property for <see cref="TypeProperty"/>
        /// </summary>
        public static readonly DependencyProperty TypeProperty = DependencyProperty.Register("Type", typeof(SliderToolTipType), typeof(SliderToolTipBehavior), new PropertyMetadata(SliderToolTipType.ProgressBar));

        private Track _track;

        /// <summary>
        /// Gets or sets the prefix.
        /// </summary>
        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        /// <summary>
        /// Gets or sets the Type.
        /// </summary>
        public SliderToolTipType Type
        {
            get => (SliderToolTipType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        /// <inheritdoc />
        protected override void OnAttached()
        {
            AssociatedObject.Loaded += AssociatedObjectOnLoaded;
            base.OnAttached();
        }

        /// <inheritdoc />
        protected override void OnDetaching()
        {
            _track.MouseMove -= TrackOnMouseMove;
            _track = null;
            base.OnDetaching();
        }

        private void AssociatedObjectOnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            AssociatedObject.Loaded -= AssociatedObjectOnLoaded;
            _track = (Track)AssociatedObject.Template.FindName("PART_Track", AssociatedObject);
            _track.MouseMove += TrackOnMouseMove;
        }

        private void TrackOnMouseMove(object sender, MouseEventArgs mouseEventArgs)
        {
            var position = mouseEventArgs.GetPosition(_track);
            var valueFromPoint = _track.ValueFromPoint(position);
            var floorOfValueFromPoint = (int)Math.Floor(valueFromPoint);

            var displayText = $"{Prefix}{floorOfValueFromPoint}";

            switch (Type)
            {
                case SliderToolTipType.ProgressBar:
                    {
                        var time = TimeSpan.FromMilliseconds(floorOfValueFromPoint);
                        displayText = time.ToString("mm\\:ss");

                        if (time >= TimeSpan.FromHours(1))
                        {
                            displayText = time.ToString("hh\\:mm\\:ss");
                        }

                        break;
                    }
                case SliderToolTipType.Volume:
                    {
                        break;
                    }
                default:
                    break;
            }

            var toolTip = $"{Prefix}{displayText}";
            ToolTipService.SetToolTip(_track, toolTip);
            ToolTipService.SetShowDuration(_track, 100000);
        }
    }
}

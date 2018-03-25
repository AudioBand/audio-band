﻿using AudioBand.Plugins;
using CSDeskBand;
using CSDeskBand.Win;
using NLog;
using Svg;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AudioBand.Connector;
using NLog.Config;
using NLog.LayoutRenderers;
using Size = System.Drawing.Size;

namespace AudioBand
{
    [Guid("957D8782-5B07-4126-9B24-1E917BAAAD64")]
    [ComVisible(true)]
    [CSDeskBandRegistration(Name = "Audio Band")]
    public partial class MainControl : CSDeskBandWin
    {
        private const int FixedWidth = 250;
        private readonly int _maxHeight = CSDeskBandOptions.TaskbarHorizontalHeightLarge;
        private readonly int _minHeight = CSDeskBandOptions.TaskbarHorizontalHeightSmall;
        private readonly SvgDocument _playButtonSvg = SvgDocument.Open<SvgDocument>(new MemoryStream(Properties.Resources.play));
        private readonly SvgDocument _pauseButtonSvg = SvgDocument.Open<SvgDocument>(new MemoryStream(Properties.Resources.pause));
        private readonly SvgDocument _nextButtonSvg = SvgDocument.Open<SvgDocument>(new MemoryStream(Properties.Resources.next));
        private readonly SvgDocument _previousButtonSvg = SvgDocument.Open<SvgDocument>(new MemoryStream(Properties.Resources.previous));
        private readonly AudioBandViewModel _audioBandViewModel = new AudioBandViewModel();
        private readonly ConnectorManager _connectorManager;
        private readonly ILogger _logger = LogManager.GetLogger("Audio Band");
        private IAudioConnector _connector;
        private CSDeskBandMenu _pluginSubMenu;
        private Image _albumArt = new Bitmap(1, 1);

        public MainControl()
        {
            InitializeComponent();

            Options.Fixed = true;
            Options.Increment = 0;
            Options.Horizontal = Size = new Size(FixedWidth, _maxHeight);
            Options.MinHorizontal = MinimumSize = new Size(FixedWidth, _minHeight);
            Options.MaxHorizontal = MaximumSize = Size;

            ResetViewModel();
            SizeChanged += OnSizeChanged;
            playPauseButton.Click += async (sender, eventArgs) => await PlayPauseButtonOnClick(sender, eventArgs);
            previousButton.Click += async (sender, eventArgs) => await PreviousButtonOnClick(sender, eventArgs);
            nextButton.Click += async (sender, eventArgs) => await NextButtonOnClick(sender, eventArgs);
            _audioBandViewModel.PropertyChanged += AudioBandViewModelOnPropertyChanged;

            nowPlayingText.DataBindings.Add("Text", _audioBandViewModel, nameof(AudioBandViewModel.NowPlayingText));
            albumArt.DataBindings.Add("Image", _audioBandViewModel, nameof(AudioBandViewModel.AlbumArt));
            audioProgress.DataBindings.Add("Value", _audioBandViewModel, nameof(AudioBandViewModel.AudioProgress));
            previousButton.DataBindings.Add("Image", _audioBandViewModel, nameof(AudioBandViewModel.PreviousButtonBitmap));
            playPauseButton.DataBindings.Add("Image", _audioBandViewModel, nameof(AudioBandViewModel.PlayPauseButtonBitmap));
            nextButton.DataBindings.Add("Image", _audioBandViewModel, nameof(AudioBandViewModel.NextButtonBitmap));

            try
            {
                _connectorManager = new ConnectorManager();
                Options.ContextMenuItems = BuildContextMenu();
            }
            catch (ReflectionTypeLoadException e)
            {
                _logger.Error(e);
                foreach (var loaderException in e.LoaderExceptions)
                {
                    _logger.Error(loaderException);
                }
                throw;
            }
        }

        static MainControl()
        {
            // Fix nlog path
            var nlogConfigFile = Path.Combine(DirectoryHelper.BaseDirectory, "NLog.config");
            if (File.Exists(nlogConfigFile))
            {
                LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigFile);
            }
        }

        private List<CSDeskBandMenuItem> BuildContextMenu()
        {
            var pluginList = _connectorManager.AudioConnectors.Select(connector =>
            {
                var item = new CSDeskBandMenuAction(connector.ConnectorName);
                item.Clicked += ConnectorMenuItemOnClicked;
                return item;
            });

            _pluginSubMenu = new CSDeskBandMenu("Audio Source", pluginList);

            return new List<CSDeskBandMenuItem>{ _pluginSubMenu };
        }

        private async void ConnectorMenuItemOnClicked(object sender, EventArgs eventArgs)
        {
            var item = (CSDeskBandMenuAction)sender;
            if (item.Checked)
            {
                item.Checked = false;
                await UnsubscribeToConnector(_connector);
                _connector = null;
                return;
            }
            // Uncheck old item and unsubscribe from the current connector
            var lastItemChecked = _pluginSubMenu.Items.Cast<CSDeskBandMenuAction>().FirstOrDefault(i => i.Text == _connector?.ConnectorName);
            if (lastItemChecked != null)
            {
                lastItemChecked.Checked = false;
            }

            await UnsubscribeToConnector(_connector);

            item.Checked = true;
            _connector = _connectorManager.AudioConnectors.First(c => c.ConnectorName == item.Text);
            await SubscribeToConnector(_connector);
        }

        private async Task SubscribeToConnector(IAudioConnector connector)
        {
            if (connector == null)
            {
                return;
            }

            connector.TrackInfoChanged += ConnectorOnTrackInfoChanged;
            connector.AlbumArtChanged += ConnectorOnAlbumArtChanged;
            connector.TrackPlaying += ConnectorOnTrackPlaying;
            connector.TrackPaused += ConnectorOnTrackPaused;
            connector.TrackProgressChanged += ConnectorOnTrackProgressChanged;
            await connector.ActivateAsync();
        }

        private async Task UnsubscribeToConnector(IAudioConnector connector)
        {
            if (connector == null)
            {
                return;
            }

            connector.TrackInfoChanged -= ConnectorOnTrackInfoChanged;
            connector.AlbumArtChanged -= ConnectorOnAlbumArtChanged;
            connector.TrackPlaying -= ConnectorOnTrackPlaying;
            connector.TrackPaused -= ConnectorOnTrackPaused;
            connector.TrackProgressChanged -= ConnectorOnTrackProgressChanged;
            await connector.DeactivateAsync();

            ResetViewModel();
        }

        private void ConnectorOnTrackProgressChanged(object o, int progress)
        {
            BeginInvoke(new Action(() => { _audioBandViewModel.AudioProgress = progress;}));
        }

        private void ConnectorOnTrackPaused(object o, EventArgs args)
        {
            BeginInvoke(new Action(() =>_audioBandViewModel.IsPlaying = false));
        }

        private void ConnectorOnTrackPlaying(object o, EventArgs args)
        {
            BeginInvoke(new Action(() => _audioBandViewModel.IsPlaying = true));
        }

        private void ConnectorOnAlbumArtChanged(object sender, AlbumArtChangedEventArgs albumArtChangedEventArgs)
        {
            _albumArt = albumArtChangedEventArgs.AlbumArt;
            BeginInvoke(new Action(() => UpdateAlbumArt(_albumArt)));
        }

        private void ConnectorOnTrackInfoChanged(object sender, TrackInfoChangedEventArgs trackInfoChangedEventArgs)
        {
            var text = BuildNowPlayingText(trackInfoChangedEventArgs.Artist, trackInfoChangedEventArgs.TrackName);
            BeginInvoke(new Action(() => _audioBandViewModel.NowPlayingText = text));
        }

        private void AudioBandViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(AudioBandViewModel.IsPlaying):
                    UpdateControlSvgs();
                    break;
                default: break;
            }
        }

        private async Task PlayPauseButtonOnClick(object sender, EventArgs eventArgs)
        {
            if (_audioBandViewModel.IsPlaying)
            {
                await (_connector?.PauseTrackAsync() ?? Task.CompletedTask);
            }
            else
            {
                await (_connector?.PlayTrackAsync() ?? Task.CompletedTask);
            }

        }

        private async Task PreviousButtonOnClick(object sender, EventArgs eventArgs)
        {
            await (_connector?.PreviousTrackAsync() ?? Task.CompletedTask);
        }

        private async Task NextButtonOnClick(object sender, EventArgs eventArgs)
        {
            await (_connector?.NextTrackAsync() ?? Task.CompletedTask);
        }

        private void OnSizeChanged(object sender, EventArgs eventArgs)
        {
            UpdateAlbumArt(_albumArt);
            UpdateControlSvgs();
        }

        private void UpdateAlbumArt(Image albumArt)
        {
            var height = mainTable.GetRowHeights().Take(2).Sum();
            mainTable.ColumnStyles[0].SizeType = SizeType.Absolute;
            mainTable.ColumnStyles[0].Width = height;

            var newAlbumArt = new Bitmap(height, height);
            using (var graphics = Graphics.FromImage(newAlbumArt))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(albumArt, 0, 0, newAlbumArt.Width, newAlbumArt.Height);
            }

            _audioBandViewModel.AlbumArt = newAlbumArt;
        }

        private void UpdateControlSvgs()
        {
            // Issues with svg
            const int padding = 3;
            var height = buttonsTable.GetRowHeights()[0] - padding;

            SvgDocument playPauseSvg = _audioBandViewModel.IsPlaying ? _pauseButtonSvg : _playButtonSvg;
            playPauseSvg.Width = playPauseButton.Width;
            playPauseSvg.Height = height;
            _audioBandViewModel.PlayPauseButtonBitmap = DrawSvg(playPauseSvg);

            _nextButtonSvg.Width = nextButton.Width;
            _nextButtonSvg.Height = height;
            _audioBandViewModel.NextButtonBitmap = DrawSvg(_nextButtonSvg);

            _previousButtonSvg.Width = previousButton.Width;
            _previousButtonSvg.Height = height;
            _audioBandViewModel.PreviousButtonBitmap = DrawSvg(_previousButtonSvg);
        }

        private Bitmap DrawSvg(SvgDocument svg)
        {
            var bmp = new Bitmap((int)svg.Width.Value, (int)svg.Height.Value);
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.High;
                svg.Draw(graphics);
                return bmp;
            }
        }

        private void ResetViewModel()
        {
            _audioBandViewModel.NowPlayingText = "";
            _audioBandViewModel.IsPlaying = false;
            _audioBandViewModel.AlbumArt = new Bitmap(1, 1);
            _audioBandViewModel.AudioProgress = 0;
        }

        private string BuildNowPlayingText(string artist, string name)
        {
            return $"{(artist == null ? "" : artist + " - ")}{name}";
        }
    }
}

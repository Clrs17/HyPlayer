#region

using HyPlayer.Classes;
using HyPlayer.HyPlayControl;
using NeteaseCloudMusicApi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

#endregion

namespace HyPlayer.Pages;

public sealed partial class RadioPage : Page, IDisposable
{
    private bool asc;
    private int i;
    private int page;
    private NCRadio Radio;
    private bool disposedValue = false;
    private Task _programLoaderTask;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private CancellationToken _cancellationToken;

    public ObservableCollection<NCSong> Songs = new();

    public RadioPage()
    {
        InitializeComponent();
        _cancellationToken = _cancellationTokenSource.Token;
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_programLoaderTask != null && !_programLoaderTask.IsCompleted)
        {
            try
            {
                _cancellationTokenSource.Cancel();
                await _programLoaderTask;
            }
            catch
            {
                Dispose();
                return;
            }
        }
        Dispose();
    }

    private async Task LoadProgram()
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        _cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.DjProgram,
                new Dictionary<string, object>
                {
                    { "rid", Radio.id },
                    { "offset", page * 30 },
                    { "asc", asc }
                });
            NextPage.Visibility = json["more"].ToObject<bool>() ? Visibility.Visible : Visibility.Collapsed;
            foreach (var jToken in json["programs"])
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var song = NCFmItem.CreateFromJson(jToken);
                song.Type = HyPlayItemType.Radio;
                song.Order = i++;
                Songs.Add(song);
            }
        }
        catch (Exception ex)
        {
            if (ex.GetType() != typeof(TaskCanceledException) && ex.GetType() != typeof(OperationCanceledException))
                Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string rid)
            try
            {
                var json1 = await Common.ncapi.RequestAsync(CloudMusicApiProviders.DjDetail,
                    new Dictionary<string, object> { { "rid", rid } });
                Radio = NCRadio.CreateFromJson(json1["djRadio"]);
            }
            catch (Exception ex)
            {
                Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
            }

        if (e.Parameter is NCRadio radio) Radio = radio;

        TextBoxRadioName.Text = Radio.name;
        TextBoxDJ.Content = Radio.DJ.name;
        TextBlockDesc.Text = Radio.desc;
        ImageRect.ImageSource =
            Common.Setting.noImage
                ? null
                : new BitmapImage(new Uri(Radio.cover + "?param=" + StaticSource.PICSIZE_SONGLIST_DETAIL_COVER));
        Songs.Clear();
        SongContainer.ListSource = "rd" + Radio.id;
        _programLoaderTask = LoadProgram();
    }

    private void NextPage_OnClickPage_OnClick(object sender, RoutedEventArgs e)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        page++;
        _programLoaderTask = LoadProgram();
    }

    private async void ButtonPlayAll_OnClick(object sender, RoutedEventArgs e)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        try
        {
            await HyPlayList.AppendNcSource("rd" + Radio.id);
            if (asc) HyPlayList.List.Reverse();
            HyPlayList.SongMoveTo(0);
        }
        catch (Exception ex)
        {
            Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
        }
    }

    private void TextBoxDJ_OnTapped(object sender, RoutedEventArgs routedEventArgs)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        Common.NavigatePage(typeof(Me), Radio.DJ.id);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        Songs.Clear();
        page = 0;
        i = 0;
        asc = !asc;
        _programLoaderTask = LoadProgram();
    }

    private async void BtnAddAll_Clicked(object sender, RoutedEventArgs e)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        await HyPlayList.AppendRadioList(Radio.id, asc);
    }

    private async void ButtonDownloadAll_OnClick(object sender, RoutedEventArgs e)
    {
        if (disposedValue) throw new ObjectDisposedException(nameof(RadioPage));
        var result = new List<NCSong>();
        try
        {
            bool? hasMore = true;
            var page = 0;
            while (hasMore is true)
                try
                {
                    var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.DjProgram,
                        new Dictionary<string, object>
                        {
                            { "rid", Radio.id },
                            { "offset", page++ * 100 },
                            { "limit", 100 },
                            { "asc", asc }
                        });
                    hasMore = json["more"]?.ToObject<bool>();
                    if (json["programs"] is not null)
                        result.AddRange(json["programs"].Select(t => (NCSong)NCFmItem.CreateFromJson(t)).ToList());
                }
                catch (Exception ex)
                {
                    Common.AddToTeachingTipLists(ex.Message,
                        (ex.InnerException ?? new Exception()).Message);
                }
        }
        catch (Exception ex)
        {
            Common.AddToTeachingTipLists(ex.Message, (ex.InnerException ?? new Exception()).Message);
        }
        DownloadManager.AddDownload(result);
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                ImageRect.ImageSource = null;
                SongContainer.Dispose();
                Songs.Clear();
                _cancellationTokenSource.Dispose();
            }

            disposedValue = true;
        }
    }

    ~RadioPage()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
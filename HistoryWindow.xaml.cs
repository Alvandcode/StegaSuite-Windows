using System.Windows;

namespace StegaSuite;

public partial class HistoryWindow : Window
{
    private readonly string _user;
    private readonly MainWindow _owner;

    public HistoryWindow(MainWindow owner, string user)
    {
        InitializeComponent();
        _owner = owner;
        _user = user;
        FlowDirection = owner.FlowDirection;
        Reload();
    }

    private void Reload()
    {
        try
        {
            Title = _owner.Tr("history");
            LblHistTitle.Text = _owner.Tr("history");
            BtnClearHist.Content = _owner.Tr("clearHistory");
            LstHist.Items.Clear();
            var list = HistoryStore.Load(_user);
            if (list.Count == 0) LstHist.Items.Add(_owner.Tr("emptyHistory"));
            else foreach (var e in list)
                LstHist.Items.Add($"{e.Time} • {_owner.Tr(e.Kind)} • {e.Detail}");
        }
        catch { }
    }

    private void BtnClearHist_Click(object sender, RoutedEventArgs e)
    {
        try { HistoryStore.Clear(_user); Reload(); } catch { }
    }

    private void BtnCloseHist_Click(object sender, RoutedEventArgs e)
    {
        try { Close(); } catch { }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Model;

namespace SafariInc.Views;

public partial class ZooView : UserControl
{
    public ZooView()
    {
        InitializeComponent();
    }

    /// <summary>Bubbling: Click eines beliebigen Geheges kommt hier an.</summary>
    private void Enclosures_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { DataContext: Enclosure enclosure } button) return;
        e.Handled = true;

        if (!enclosure.IsUnlocked)
        {
            button.RaiseEvent(new EnclosureEventArgs(enclosure));
        }
        else if (enclosure.Animal is { } animal)
        {
            SoundService.Play(animal.Species.Id, animal.Species.Sound);
            Wiggle(button);
        }
        else
        {
            button.RaiseEvent(new GameMessageEventArgs("Dieses Gehege ist noch leer. Ab auf Safari!"));
        }
    }

    private static void Wiggle(UIElement element)
    {
        // Neue Transform statt der aus dem Template – Template-Objekte sind eingefroren.
        var scale = new ScaleTransform();
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;

        var bounce = new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(130))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
    }
}

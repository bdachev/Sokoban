using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Windows.Threading;

namespace Sokoban
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // microcosmos - 21
        const string _boardInfo =
                                    "  #####\n" +
                                    "  #   ###\n" +
                                    "###*# $ #\n" +
                                    "# $ @ # #\n" +
                                    "# # ..  #\n" +
                                    "# . #$###\n" +
                                    "##$.  #\n" +
                                    " #  ###\n" +
                                    " ####";//\n ####";

        SokobanLogic.Board _board = new SokobanLogic.Board();
        public MainWindow()
        {
            InitializeComponent();

            _board.Init(_boardInfo);

            DataContext = _board;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            switch (e.Key)
            {
                case Key.Up:
                    _board.Move(SokobanLogic.MoveType.Up);
                    break;
                case Key.Down:
                    _board.Move(SokobanLogic.MoveType.Down);
                    break;
                case Key.Left:
                    _board.Move(SokobanLogic.MoveType.Left);
                    break;
                case Key.Right:
                    _board.Move(SokobanLogic.MoveType.Right);
                    break;
                case Key.Z:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        _board.UndoMove();
                    }
                    break;
            }
        }
        //delegate void CancellableAction(Func<bool> canceler);
        //private void Solve_Click(object sender, RoutedEventArgs e)
        //{
        //    bool cancel = false;
        //    CancellableAction solve = _board.Solve;
        //    var result = solve.BeginInvoke(() => cancel, null, null);

        //    CancelWindow c = new CancelWindow(() => result.IsCompleted, 100);
        //    c.Owner = this;
        //    if (c.ShowDialog() == false && !result.IsCompleted)
        //    {
        //        cancel = true;
        //        result.AsyncWaitHandle.WaitOne();
        //    }
        //}

        private void Solve_Click(object sender, RoutedEventArgs e)
        {
            var board = new SokobanLogic.SimpleBoard();
            //var board = _board;
            board.Init(_boardInfo);
            SokobanLogic.MoveType mt = SokobanLogic.MoveType.Start;
            CancelWindow c = new CancelWindow(() =>
            {
                if (board.IsSolved() || !board.MoveNext(ref mt))
                    return true;
                mt = board.IsInDeadLock() ? SokobanLogic.MoveType.Stop : SokobanLogic.MoveType.Start;
                return false;
            }, 0);
            c.Owner = this;
            if (c.ShowDialog() == false || board.IsSolved())
                MessageBox.Show(board.Solution(), "Solution");
        }
    }

    public class CellTypeConverter : IValueConverter
    {
        public Brush BrushWall { get; set; }
        public Brush BrushEmpty { get; set; }
        public Brush BrushBox { get; set; }
        public Brush BrushBoxInPlace { get; set; }
        public Brush BrushBuddy { get; set; }
        public Brush BrushBuddyInPlace { get; set; }
        public Brush BrushPlace { get; set; }

        static public CellTypeConverter Instance
        {
            get { return _instance; }
        }
        static CellTypeConverter _instance = new CellTypeConverter();
        public CellTypeConverter()
        {
            BrushWall = new SolidColorBrush(Colors.DarkGray);
            BrushEmpty = new SolidColorBrush(Colors.Transparent);
            BrushBox = new SolidColorBrush(Colors.Yellow);
            BrushBoxInPlace = new SolidColorBrush(Colors.YellowGreen);
            BrushBuddy = new SolidColorBrush(Colors.Red);
            BrushBuddyInPlace = new SolidColorBrush(Colors.Red);
            BrushPlace = new SolidColorBrush(Colors.LightGreen);
        }

        #region IValueConverter Members

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Print(value == null ? "value=<null>" : string.Format("value is {0}", value.GetType()));
            if (value == null || !(value is SokobanLogic.CellType))
                return DependencyProperty.UnsetValue;

            switch ((SokobanLogic.CellType)value)
            {
                case SokobanLogic.CellType.Wall:
                    return BrushWall;

                case SokobanLogic.CellType.Empty:
                    return BrushEmpty;

                case SokobanLogic.CellType.Buddy:
                    return BrushBuddy;

                case SokobanLogic.CellType.Buddy | SokobanLogic.CellType.Place:
                    return BrushBuddyInPlace;

                case SokobanLogic.CellType.Box:
                    return BrushBox;

                case SokobanLogic.CellType.Box | SokobanLogic.CellType.Place:
                    return BrushBoxInPlace;

                case SokobanLogic.CellType.Place:
                    return BrushPlace;

                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        #endregion
    }
}

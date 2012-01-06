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
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;

namespace Sokoban
{
    /// <summary>
    /// Interaction logic for CancelWindow.xaml
    /// </summary>
    public partial class CancelWindow : Window
    {
        int _counter;
        DispatcherTimer _timer = new DispatcherTimer();
        Func<bool> _canClose;

        public CancelWindow(Func<bool> canClose, int timeoutMS)
        {
            _canClose = canClose;
            Tag = _counter;
            
            InitializeComponent();

            _timer.Tick += _timer_Tick;
            _timer.Interval = TimeSpan.FromMilliseconds(timeoutMS);
            _timer.Start();

            Closed += (o, e) =>
            {
                _timer.Stop();
                _timer.Tick -= _timer_Tick;
            };
        }


        void _timer_Tick(object sender, EventArgs e)
        {
            Tag = ++_counter;
            if (_canClose != null && _canClose())
                DialogResult = true;
        }
    }
}

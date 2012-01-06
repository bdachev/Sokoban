using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sokoban
{
    static class SokobanLogic
    {
        [Flags]
        public enum CellType
        {
            Empty,
            Wall = 1, // do not combine with others
            Place = 2, // place for the box
            Buddy = 4, // worker
            Box = 8, // box to be moved
        }

        public class CellLegend
        {
            public readonly CellType _type;
            public readonly char[] _chars;

            public CellLegend(CellType type, params char[] chars)
            {
                _type = type;
                _chars = chars;
                Debug.Assert(chars.Length >= 2);
            }
        }

        public enum MoveType
        {
            Start = -1,
            Left,
            Up,
            Right,
            Down,
            Stop
        }

        public struct UndoStep
        {
            public readonly MoveType _type;
            public readonly bool _boxMoved;
            public readonly string _boardInfo;

            public UndoStep(MoveType type, bool boxMoved, string boardInfo)
            {
                _type = type;
                _boxMoved = boxMoved;
                _boardInfo = boardInfo;
            }

            public char ToChar()
            {
                char ch;
                switch (_type)
                {
                    case MoveType.Up:
                        ch = 'u';
                        break;
                    case MoveType.Down:
                        ch = 'd';
                        break;
                    case MoveType.Left:
                        ch = 'l';
                        break;
                    case MoveType.Right:
                        ch = 'r';
                        break;
                    default:
                        throw new ApplicationException("Invalid move");
                }
                if (_boxMoved)
                    ch = char.ToUpper(ch);
                return ch;
            }
        }

        public interface iUndo
        {
            void PushStep(UndoStep step);
            UndoStep PopStep();
            UndoStep PeekStep();
            IEnumerable<UndoStep> Steps { get; }
        }

        public interface iBoard : iUndo
        {
            int Width { get; }
            int Height { get; }
            int BuddyX { get; set; }
            int BuddyY { get; set; }
            CellType this[int x, int y] { get; set; }
            IEnumerable<CellType> Cells { get; }
            void Init(CellType[,] cells);
        }

        static readonly CellLegend[] _legendMap = 
        {
            new CellLegend(CellType.Empty, ' ', ' '),
            new CellLegend(CellType.Wall, '#', '#'),
            new CellLegend(CellType.Buddy, '@', 'p'),
            new CellLegend(CellType.Buddy | CellType.Place, '+', 'P'),
            new CellLegend(CellType.Box, '$', 'b'),
            new CellLegend(CellType.Box | CellType.Place, '*', 'B'),
            new CellLegend(CellType.Place, '.', 'o'),
            new CellLegend(CellType.Empty, '-', '_'),
        };

        public static CellType FromChar(char t)
        {
            foreach (var legend in _legendMap)
            {
                if (legend._chars.Contains(t))
                    return legend._type;
            }
            throw new ArgumentException("Unrecognized character in board description", "t");
        }

        public static bool IsEmpty(CellType t)
        {
            return t == CellType.Empty || t == CellType.Place;
        }

        public static bool Is(CellType t, CellType mask)
        {
            return (t & mask) == mask;
        }

        public static MoveType CalcMoveType(int dx, int dy)
        {
            if (dx > 0)
            {
                return MoveType.Right;
            }
            else if (dx < 0)
            {
                return MoveType.Left;
            }
            else if (dy > 0)
            {
                return MoveType.Down;
            }
            else
            {
                Debug.Assert(dy < 0);
                return MoveType.Up;
            }
        }

        public static void Move(this MoveType mt, ref int x, ref int y)
        {
            switch (mt)
            {
                case MoveType.Up:
                    y--;
                    break;
                case MoveType.Down:
                    y++;
                    break;
                case MoveType.Left:
                    x--;
                    break;
                case MoveType.Right:
                    x++;
                    break;
                default:
                    throw new ApplicationException("Invalid move type");
            }
        }

        public static string GetBoardInfo(this iBoard b, bool letters)
        {
            int width = b.Width, height = b.Height;
            if (width > 0 && height > 0)
            {
                int index = letters ? 1 : 0;
                StringBuilder sb = new StringBuilder((width + 1) * height);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var ct = b[x, y];
                        var ctl = _legendMap.FirstOrDefault(ctl_ => ctl_._type == ct);
                        if (ctl == null)
                        {
                            throw new ApplicationException(string.Format("Incorrect cell '{0}' type encountered in board", ct));
                        }
                        sb.Append(ctl._chars[index]);
                    }
                    sb.Append('\n');
                }
                return sb.ToString();
            }
            return string.Empty;
        }

        static public void Init(this iBoard b, string boardInfo)
        {
            b.Init(null);
            b.BuddyX = b.BuddyY = 0;
            int boxes = 0;
            int places = 0;
            if (boardInfo == null)
            {
                throw new ArgumentNullException("board", "Null passed as board description");
            }
            if (string.IsNullOrEmpty(boardInfo))
            {
                throw new ArgumentException("Empty string passed as board description", "board");
            }
            string[] lines = boardInfo.Split('\n', '|');
            int height = lines.Length;
            if (height < 3)
            {
                throw new ArgumentException("Board description contains too few lines", "board");
            }
            int width = lines.Max(l => l.Length);
            if (width < 3)
            {
                throw new ArgumentException("Board description contains too few columns", "board");
            }
            var cells = new CellType[width, height];
            for (int y = 0; y < height; y++)
            {
                string line = lines[y];
                for (int x = 0; x < line.Length; x++)
                {
                    CellType t = FromChar(line[x]);
                    if (t != CellType.Empty && t != CellType.Wall && (x == 0 || x == width - 1 || y == 0 || y == height - 1))
                    {
                        throw new ArgumentException("Buddy, box or place on border", "board");
                    }
                    cells[x, y] = t;
                    if (Is(t, CellType.Buddy))
                    {
                        if (b.BuddyX > 0 && b.BuddyY > 0)
                        {
                            throw new ArgumentException("More than one buddy on board", "board");
                        }
                        b.BuddyX = x;
                        b.BuddyY = y;
                    }
                    if (Is(t, CellType.Box))
                        boxes++;
                    if (Is(t, CellType.Place))
                        places++;
                }
            }
            if (boxes != places)
            {
                throw new ArgumentException("Number of boxes not equal to number of places", "board");
            }
            if (boxes == 0)
            {
                throw new ArgumentException("No boxes on board", "board");
            }
            if (b.BuddyX == 0 && b.BuddyY == 0)
            {
                throw new ArgumentException("No buddy on board", "board");
            }
            b.Init(cells);
        }

        static public bool Move(this iBoard b, MoveType moveType)
        {
            int dx = 0, dy = 0;
            moveType.Move(ref dx, ref dy);

            int bx = b.BuddyX, by = b.BuddyY;
            int nx = bx+ dx, ny = by + dy;
            // on border check
            if (dx < 0 && nx <= 0 || dx > 0 && nx >= b.Width - 1)
                return false;
            if (dy < 0 && ny <= 0 || dy > 0 && ny >= b.Height - 1)
                return false;

            if (IsEmpty(b[nx, ny]))
            {
                string boardInfo = b.GetBoardInfo(true);
                b[bx,by] &= ~CellType.Buddy;
                b[nx, ny] |= CellType.Buddy;
                b.BuddyX = nx;
                b.BuddyY = ny;
                b.PushStep(new UndoStep(moveType, false, boardInfo));
                return true;
            }
            if (Is(b[nx, ny], CellType.Box))
            {
                int nbx = nx + dx, nby = ny + dy;
                // on border check
                if (dx < 0 && nbx <= 0 || dx > 0 && nbx >= b.Width - 1)
                    return false;
                if (dy < 0 && nby <= 0 || dy > 0 && nby >= b.Height - 1)
                    return false;
                if (IsEmpty(b[nbx, nby]))
                {
                    string boardInfo = b.GetBoardInfo(true);
                    b[nx, ny] &= ~CellType.Box;
                    b[nbx, nby] |= CellType.Box;
                    b[bx, by] &= ~CellType.Buddy;
                    b[nx, ny] |= CellType.Buddy;
                    b.BuddyX = nx;
                    b.BuddyY = ny;
                    b.PushStep(new UndoStep(moveType, true, boardInfo));
                    return true;
                }
            }
            return false;
        }

        static public void UndoMove(this iBoard b)
        {
            if (!b.Steps.Any())
                return;

            UndoStep step = b.PopStep();
            int dx = 0, dy = 0;
            step._type.Move(ref dx, ref dy);
            int bx = b.BuddyX, by = b.BuddyY;
            Debug.Assert(Is(b[bx, by], CellType.Buddy));
            b[bx, by] &= ~CellType.Buddy;
            Debug.Assert(IsEmpty(b[bx - dx, by - dy]));
            b[bx - dx, by - dy] |= CellType.Buddy;
            if (step._boxMoved)
            {
                int obx = bx + dx, oby = by + dy;
                Debug.Assert(Is(b[obx, oby], CellType.Box));
                b[obx, oby] &= ~CellType.Box;
                b[bx, by] |= CellType.Box;
            }
            b.BuddyX -= dx;
            b.BuddyY -= dy;
        }
        static public bool IsSolved(this iBoard b)
        {
            var ctm = CellType.Box | CellType.Place;
            return b.Cells.All(ct => (ct & CellType.Place) != CellType.Place || (ct & ctm) == ctm);
        }

        static public string Solution(this iBoard b)
        {
            StringBuilder sb = new StringBuilder(b.Steps.Count());
            foreach (var step in b.Steps)
            {
                sb.Append(step.ToChar());
            }
            return sb.ToString();
        }

        static public void Solve(this iBoard b, Func<bool> canceler)
        {
            MoveType lastMove = MoveType.Start;
            while (!canceler() && !b.IsSolved() && b.MoveNext(ref lastMove))
            {
                lastMove = b.IsInDeadLock() ? MoveType.Stop : MoveType.Start;
                Thread.Sleep(1000);
            }
        }

        static public bool IsInDeadLock(this iBoard b)
        {
            string boardInfo = b.GetBoardInfo(true);
            if (b.Steps.Any(us => us._boardInfo == boardInfo))
            {
                return true;
            }
            for (int y = 1; y < b.Height - 1; y++)
            {
                for (int x = 1; x < b.Width - 1; x++)
                {
                    if (IsBoxNotInPlace(b[x, y]))
                    {
                        bool u = b.CanNotBeMoved(x, y, MoveType.Up);
                        bool d = b.CanNotBeMoved(x, y, MoveType.Down);
                        bool l = b.CanNotBeMoved(x, y, MoveType.Left);
                        bool r = b.CanNotBeMoved(x, y, MoveType.Right);
                        if ((u || d) && (l || r))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        static bool IsBoxNotInPlace(CellType t)
        {
            return (t & (CellType.Box | CellType.Place)) == CellType.Box;
        }

        static bool CanNotBeMoved(this iBoard b, int x, int y, MoveType mt)
        {
            mt.Move(ref x, ref y);
            CellType t = b[x, y];

            if (t == CellType.Wall)
                return true;

            if (Is(t, CellType.Box))
            {
                bool u = mt == MoveType.Down || b.CanNotBeMoved(x, y, MoveType.Up);
                bool d = mt == MoveType.Up || b.CanNotBeMoved(x, y, MoveType.Down);
                bool l = mt == MoveType.Right || b.CanNotBeMoved(x, y, MoveType.Left);
                bool r = mt == MoveType.Left || b.CanNotBeMoved(x, y, MoveType.Right);
                if ((u || d) && (l || r))
                {
                    return true;
                }
            }

            return false;
        }

        static public bool MoveNext(this iBoard b, ref MoveType lastMove)
        {
            while (lastMove < MoveType.Stop)
            {
                lastMove = lastMove + 1;
                switch (lastMove)
                {
                    case MoveType.Left:
                    case MoveType.Up:
                    case MoveType.Right:
                    case MoveType.Down:
                        if (b.Move(lastMove))
                            return true;
                        break;
                }
            }

            if (b.Steps.Any())
            {
                UndoStep last = b.PeekStep();
                lastMove = last._type;
                b.UndoMove();
                return b.MoveNext(ref lastMove);
            }
            else
            {
                return false;
            }
        }

        public class SimpleBoard : iBoard
        {
            Stack<UndoStep> _undoStack = new Stack<UndoStep>();
            public void PushStep(UndoStep step) { _undoStack.Push(step); }
            public UndoStep PopStep() { return _undoStack.Pop(); }
            public UndoStep PeekStep() { return _undoStack.Peek(); }
            public IEnumerable<UndoStep> Steps { get { return _undoStack; } }

            public int Width { get { return _cells == null ? 0 : _cells.GetLength(0); } }
            public int Height { get { return _cells == null ? 0 : _cells.GetLength(1); } }
            public int BuddyX { get; set; }
            public int BuddyY { get; set; }
            CellType[,] _cells;

            public void Init(CellType[,] cells)
            {
                _cells = cells;
            }
            public CellType this[int x, int y]
            {
                get { return _cells == null ? CellType.Empty : _cells[x, y]; }
                set { if (_cells != null) _cells[x, y] = value; }
            }
            public IEnumerable<CellType> Cells
            {
                get
                {
                    if (_cells == null)
                    {
                        yield break;
                    }
                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            yield return _cells[x, y];
                        }
                    }
                }
            }
        }

        public class Board : INotifyPropertyChanged, iBoard
        {
            public ObservableCollection<CellType> Cells { get { return _cells; } }
            ObservableCollection<CellType> _cells;
            public int Width { get { return _width; } }
            public int Height { get { return _height; } }
            int _width, _height;
            public int BuddyX { get; set; }
            public int BuddyY { get; set; }

            Stack<UndoStep> _undoStack = new Stack<UndoStep>();
            public void PushStep(UndoStep step) { _undoStack.Push(step); NotifyPropertyChanged("Moves"); }
            public UndoStep PopStep() { UndoStep step = _undoStack.Pop(); NotifyPropertyChanged("Moves"); return step; }
            public UndoStep PeekStep() { return _undoStack.Peek(); }
            public IEnumerable<UndoStep> Steps { get { return _undoStack; } }
            public int Moves { get { return _undoStack.Count; } }

            public void Init(CellType[,] cells)
            {
                _undoStack.Clear();
                NotifyPropertyChanged("Moves");
                _width = cells == null ? 0 : cells.GetLength(0);
                NotifyPropertyChanged("Width");
                _height = cells == null ? 0 : cells.GetLength(1);
                NotifyPropertyChanged("Height");
                if (cells == null)
                {
                    _cells = null;
                }
                else
                {
                    _cells = new ObservableCollection<CellType>();
                    for (int y = 0; y < _height; y++)
                    {
                        for (int x = 0; x < _width; x++)
                        {
                            _cells.Add(cells[x, y]);
                        }
                    }
                }
                NotifyPropertyChanged("Cells");
            }

            public CellType this[int x, int y]
            {
                get
                {
                    if (x < 0 || x >= _width)
                        throw new ArgumentOutOfRangeException("x");
                    if (y < 0 || y >= _height)
                        throw new ArgumentOutOfRangeException("y");
                    return _cells[x + y * _width];
                }
                set
                {
                    if (x < 0 || x >= _width)
                        throw new ArgumentOutOfRangeException("x");
                    if (y < 0 || y >= _height)
                        throw new ArgumentOutOfRangeException("y");
                    _cells[x + y * _width] = value;
                }
            }
            IEnumerable<CellType> iBoard.Cells { get { return _cells; } }

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            protected void NotifyPropertyChanged(string propName)
            {
                var propertyChanged = PropertyChanged;
                if (propertyChanged != null)
                {
                    propertyChanged(this, new PropertyChangedEventArgs(propName));
                }
            }
            #endregion
        }
    }
}

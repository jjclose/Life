using System;
using System.IO;
using System.Collections.Generic;
using System.Text;


/**
 * Life
 * This plays the game of Conway's "Life" cellular automata simulation.
 * 
 * The program inputs a file describing the state of the cells on the game
 * "board", using the Life1.06 format.  It runs for a number of iterations
 * (default 10) and then prints out the state of the board in the same
 * Life1.06 format.
 * 
 * There are many different ways that this could be implemented.  Perhaps the 
 * most interesting ways that I did NOT pursue would be
 * 1. Use the GPU to parallelize the cellular neighbor computations. 
 * OR
 * 2. Use multiple number spaces to stitch together a mosaic of grids, each 
 * of which would represent the maximum number space possible, in effect 
 * to simulate an infinite grid.
 * 
 * The basic concept behind the algorithm used here relies on two things:
 * First, it uses a sparse representation of the cells, using a dictionary to
 * reference cells by their coordinate pairs, since representing them in a
 * spatial grid would be impossible given the numerical space.
 * Second, the algorithm does not explicitly check all the empty spaces 
 * for possible cell births.  Instead, it uses only the live cells to 
 * determine adjacency counts.  The reasoning is that it is wasteful to check
 * potentially many empty cells and count all of their neighbors (which are also 
 * often empty).  Instead check only the known live cells since any births will
 * be produced by these anyway.
 *  
 */

namespace Life
{
    public class GameState
    {
        // not really used yet in the current implementation but am
        // going to use these to keep track of the regions of interest
        private long _minX = long.MaxValue;
        private long _maxX = long.MinValue;
        private long _minY = long.MaxValue;
        private long _maxY = long.MinValue;

        private long _minXVal = long.MaxValue;
        private long _maxXVal = long.MinValue;
        private long _minYVal = long.MaxValue;
        private long _maxYVal = long.MinValue;

        public (long X, long Y) MinBounds
        {
            get => (_minX, _minY);
        }
        public (long X, long Y) MaxBounds
        {
            get => (_maxX, _maxY);
        }

        public (long X, long Y) MinValue
        {
            get => (_minXVal, _minYVal);
        }
        public (long X, long Y) MaxValue
        {
            get => (_maxXVal, _maxYVal);
        }

        public Dictionary<(long, long), int> Cells { get; set; }

        public GameState(long xmin, long ymin, long xmax, long ymax)
        {
            _minX = xmin;
            _maxX = xmax;
            _minY = ymin;
            _maxY = ymax;

            _minXVal = xmin;
            _maxXVal = xmax;
            _minYVal = ymin;
            _maxYVal = ymax;

            Cells = new Dictionary<(long, long), int>();
        }

        public void PopulateCell(long x, long y)
        {
            (long x, long y) loc = (x, y);
            if (!Cells.ContainsKey(loc))
            {
                Cells.Add(loc, 0);

                if (x < _minXVal) _minXVal = x;
                if (x > _maxXVal) _maxXVal = x;
                if (y < _minYVal) _minYVal = y;
                if (y > _maxYVal) _maxYVal = y;
            }
        }

        public void KillCell(long x, long y)
        {
            // TBD readjust/possibly shrink value bounds
            Cells.Remove((x, y));
        }

        public void DoWithAdjacentCells(long x, long y, Action<long, long> func)
        {
            if (x > _minX)
            {
                func(x - 1, y);
                if (y > _minY)
                {
                    func(x - 1, y - 1);
                }
                if (y < _maxY)
                {
                    func(x - 1, y + 1);
                }
            }
            if (x < _maxX)
            {
                func(x + 1, y);
                if (y > _minY)
                {
                    func(x + 1, y - 1);
                }
                if (y < _maxY)
                {
                    func(x + 1, y + 1);
                }
            }
            if (y > _minY)
            {
                func(x, y - 1);
            }
            if (y < _maxY)
            {
                func(x, y + 1);
            }
        }

        public bool Read(TextReader stream)
        {
#if DEBUG
            Console.WriteLine("Reading file ...");
#endif
            string line;
            int count = 0;
            line = stream.ReadLine();
            if (line == "#Life 1.06")
            {
                while ((line = stream.ReadLine()) != null)
                {
                    string[] tokens = line.Split();
                    // if incorrectly formatted, bail on this line
                    if (tokens != null && tokens.Length == 2)
                    {
                        long val1, val2;
                        if (Int64.TryParse(tokens[0], out val1) && Int64.TryParse(tokens[1], out val2))
                        {
                            PopulateCell(val1, val2);
                            count++;
                        }
                    }
                }
                if (count > 0) return true;
            }
            return false;
        }

        public void PrintCellList()
        {
            Console.WriteLine("#Life 1.06");
            Dictionary<(long x, long y), int>.KeyCollection keys = Cells.Keys;
            foreach ((long xcoord, long ycoord) in Cells.Keys)
            {
                Console.WriteLine("{0} {1}", xcoord, ycoord);
            }
        }

        public void PrintGrid(int iteration)
        {
            Console.WriteLine("n={0}", iteration);
            // only for testing, and only works if testing coordinates are in int32 bounds
            int XDim = (int)(MaxBounds.X - MinBounds.X + 1);
            int YDim = (int)(MaxBounds.Y - MinBounds.Y + 1);
            Console.WriteLine();

            int[,] grid = new int[YDim, XDim];
            // populate with elements
            Dictionary<(long x, long y), int>.KeyCollection keys = Cells.Keys;
            foreach ((long xcoord, long ycoord) in Cells.Keys)
            {
                grid[(int)(ycoord - MinBounds.Y), (int)(xcoord - MinBounds.X)] = 1;
            }
            string emptyLine = "";
            emptyLine = emptyLine.PadLeft(XDim, '.');
            for (int y = YDim-1; y>=0; y--)
            {
                StringBuilder line = new StringBuilder(emptyLine);
                for (int x = 0; x<XDim; x++)
                {
                    if (grid[y, x] > 0)
                        line[x] = 'O';
                }
                Console.Write(line.ToString());
                Console.WriteLine(" {0}", y+MinBounds.Y);
            }
        }
    }

    public struct CellStatus
    {
        public byte count; // count of adjacent populated cells
        public bool isAlive;  // whether this cell was alive at start of this iteration
        public CellStatus(byte c, bool alive) { count = c; isAlive = alive; }
        public CellStatus(byte c) { count = c; isAlive = false; }
    }

    public class LifeGame
    {
        private GameState _gameState;  // current "board" state
        public GameState State { get => _gameState; }

        private Dictionary<(long, long), CellStatus> _adjBuffer; // stores count of adjacency 
        public Dictionary<(long, long), CellStatus> Adjacencies
        {
            get => _adjBuffer;
        }

        public LifeGame(long xmin, long ymin, long xmax, long ymax, TextReader instream)
        {
            _gameState = new GameState(xmin, ymin, xmax, ymax);
            _adjBuffer = new Dictionary<(long, long), CellStatus>();
            _gameState.Read(instream);
        }

        public void PrintAdjacencies()
        {
#if DEBUG
            Console.WriteLine("Adjacencies:");
#endif
            foreach (var loc in Adjacencies.Keys)
            {
                (long x, long y) = loc;
                CellStatus cellData = Adjacencies[loc];
#if DEBUG
                Console.WriteLine("Adj x, y ({0}, {1}) = {2}", x, y, cellData.count);
#endif
            }
        }

        private bool CalcLifeAndDeathChanges()
        {
#if DEBUG
            Console.WriteLine("CalcLifeAndDeath:");
#endif
            bool changedState = false;
            foreach (var loc in Adjacencies.Keys)
            {
                (long x, long y) = loc;
                CellStatus cellData = Adjacencies[loc];
                int count = cellData.count;
                if (cellData.isAlive && (count < 2 || count > 3))
                {
#if DEBUG
                    Console.WriteLine("Kill ({0},{1})", x, y);
#endif
                    State.KillCell(x, y);
                    changedState = true;
                }
                if (!cellData.isAlive && count == 3)
                {
#if DEBUG
                    Console.WriteLine("Pop ({0},{1})", x, y);
#endif
                    State.PopulateCell(x, y);
                    changedState = true;
                }
            }
            return changedState;
        }

        public bool RunStep()
        {
            Action<long, long> IncrementAdjacent =
            (x, y) =>
            {
                (long x, long y) p = (x, y);
                if (_adjBuffer.ContainsKey(p))
                {
                    CellStatus cellData = _adjBuffer[p];
                    cellData.count = (byte)(cellData.count + 1);
                    _adjBuffer[p] = cellData;
                }
                else
                {
                    CellStatus cellData = new CellStatus(1);
                    _adjBuffer.Add(p, cellData);
                }
            };
            Adjacencies.Clear();

            // for each alive cell, record it and then increment all
            // adjacent cells since this live one contributes one to them
            Dictionary<(long x, long y), int> cells = State.Cells;
            foreach (var loc in cells.Keys)
            {
                if (Adjacencies.ContainsKey(loc))
                {
                    // just ensure this is marked alive
                    CellStatus cellData = Adjacencies[loc];
                    cellData.isAlive = true; 
                    Adjacencies[loc] = cellData;
                }
                else
                {
                    // if this doesn't exist in Adjacencies yet, then it hasn't
                    // yet been recorded by a neighbor so it has count 0
                    CellStatus cellData = new CellStatus(0, true);
                    Adjacencies.Add(loc, cellData);
                }
                (long x, long y) = loc;
                State.DoWithAdjacentCells(x, y, IncrementAdjacent);
            }
            PrintAdjacencies();
            return CalcLifeAndDeathChanges();
        }

        public void Run(int steps)
        {
            int n = 0;
            bool changedState = true;
            while (n < steps && changedState)
            {
                changedState = RunStep();
                n++;
            }
        }

        public void PrintCellList()
        {
            State.PrintCellList();
        }

        public void PrintGrid(int iteration)
        {
            State.PrintGrid(iteration);
        }
    }

    class MainClass
    {
        public const string DEFAULT_INPUT_FILENAME = "../../test.lif";
        public const int DEFAULT_ITERATIONS = 10;

        public static void Main(string[] args)
        {
            string filename = DEFAULT_INPUT_FILENAME;
            int iterations = DEFAULT_ITERATIONS;

            TextReader instream = Console.In;
            int argnum = 0;
            while (argnum < args.Length)
            {
                // use a provided filename or default test file
                if (args[argnum] == "-f")
                {
                    if (++argnum < args.Length)
                    {
                        filename = args[argnum];
                        instream = new StreamReader(filename);
                    }
                }
                else if (args[argnum] == "-n")
                {
                    if (++argnum < args.Length)
                    {
                        int val;
                        if (Int32.TryParse(args[argnum], out val))
                        {
                            iterations = val;
                        }
                    }
                }
                argnum++;
            }

            LifeGame lg = new LifeGame(Int64.MinValue, Int64.MinValue, Int64.MaxValue, Int64.MaxValue, instream);
            lg.PrintCellList();
            lg.Run(iterations);
            lg.PrintCellList();
        }
    }
}

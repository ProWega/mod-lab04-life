﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Drawing;

namespace cli_life
{
    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        private bool IsAliveNext;

        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Where(x => x.IsAlive).Count();
            if (IsAlive)
            {
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            }
            else
            {
                IsAliveNext = liveNeighbors == 3;
            }
        }

        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }

    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;

        public int Columns { get { return Cells.GetLength(0); } }
        public int Rows { get { return Cells.GetLength(1); } }
        public int Width { get { return Columns * CellSize; } }
        public int Height { get { return Rows * CellSize; } }

        public Board(int columns, int rows, int cellSize, bool[,] initialState = null)
        {
            CellSize = cellSize;
            Cells = new Cell[columns, rows];

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Cells[x, y] = new Cell();
                }
            }

            ConnectNeighbors();

            if (initialState != null)
            {
                LoadInitialState(initialState);
            }
        }

        private readonly Random rand = new Random();

        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
            {
                cell.IsAlive = rand.NextDouble() < liveDensity;
            }
        }

        public void Advance()
        {
            foreach (var cell in Cells)
            {
                cell.DetermineNextLiveState();
            }

            foreach (var cell in Cells)
            {
                cell.Advance();
            }
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = (x > 0) ? x - 1 : Columns - 1;
                    int xR = (x < Columns - 1) ? x + 1 : 0;

                    int yT = (y > 0) ? y - 1 : Rows - 1;
                    int yB = (y < Rows - 1) ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }

        private void LoadInitialState(bool[,] initialState)
        {
            int columns = initialState.GetLength(0);
            int rows = initialState.GetLength(1);

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Cells[x, y].IsAlive = initialState[x, y];
                }
            }
        }

        public bool IsStable(Board previousBoard)
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (Cells[x, y].IsAlive != previousBoard.Cells[x, y].IsAlive)
                        return false;
                }
            }
            return true;
        }

        public int SimulateUntilStable()
        {
            Board previousBoard = null;
            int generations = 0;
            while (true)
            {
                Board currentBoard = Clone();
                Advance();
                generations++;
                if (previousBoard != null && IsStable(previousBoard))
                {
                    break;
                }
                previousBoard = currentBoard;
            }
            return generations;
        }

        public Board Clone()
        {
            var clone = new Board(Columns, Rows, CellSize);
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    clone.Cells[x, y].IsAlive = Cells[x, y].IsAlive;
                }
            }
            return clone;
        }
    }

    public class Settings
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int CellSize { get; set; }
        public double LiveDensity { get; set; }

        public static Settings Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Settings>(json);
        }
    }

    public class GameState
    {
        [JsonConverter(typeof(BoolArrayConverter))]
        public bool[,] Cells { get; set; }

        public static GameState FromBoard(Board board)
        {
            int columns = board.Columns;
            int rows = board.Rows;
            var cells = new bool[columns, rows];

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; rows < rows; y++)
                {
                    cells[x, y] = board.Cells[x, y].IsAlive;
                }
            }

            return new GameState { Cells = cells };
        }

        public void ToBoard(Board board)
        {
            int columns = Cells.GetLength(0);
            int rows = Cells.GetLength(1);

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    board.Cells[x, y].IsAlive = Cells[x, y];
                }
            }
        }

        public static GameState Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GameState>(json);
        }

        public void Save(string filePath)
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }

    public class BoolArrayConverter : JsonConverter<bool[,]>
    {
        public override bool[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonDocument = JsonDocument.ParseValue(ref reader);
            var rootElement = jsonDocument.RootElement;

            int rows = rootElement.GetArrayLength();
            int columns = rootElement[0].GetArrayLength();

            var result = new bool[rows, columns];
            int row = 0;

            foreach (var jsonRow in rootElement.EnumerateArray())
            {
                int column = 0;
                foreach (var jsonValue in jsonRow.EnumerateArray())
                {
                    result[row, column] = jsonValue.GetBoolean();
                    column++;
                }
                row++;
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, bool[,] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            for (int i = 0; i < value.GetLength(0); i++)
            {
                writer.WriteStartArray();
                for (int j = 0; j < value.GetLength(1); j++)
                {
                    writer.WriteBooleanValue(value[i, j]);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }
    }

    class Program
    {
        static Board board;

        static void LoadSettings(string filePath)
        {
            Settings settings = Settings.Load(filePath);
            board = new Board(settings.Width / settings.CellSize, settings.Height / settings.CellSize, settings.CellSize);
        }

        static void SaveState(string filePath)
        {
            GameState gameState = GameState.FromBoard(board);
            gameState.Save(filePath);
        }

        static void LoadState(string filePath)
        {
            GameState gameState = GameState.Load(filePath);
            board = new Board(gameState.Cells.GetLength(0), gameState.Cells.GetLength(1), 1, gameState.Cells);
        }

        static void Reset()
        {
            //board = new Board(50, 20, 1, 0.5);
        }

        static void Render()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    var cell = board.Cells[col, row];
                    Console.Write(cell.IsAlive ? '*' : ' ');
                }
                Console.WriteLine();
            }
        }

        static void Main(string[] args)
        {
            string settingsFile = "settings.json";

            LoadSettings(settingsFile);

            List<int> densities = new List<int> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            List<int> generationsList = new List<int>();

            foreach (int density in densities)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    board.Randomize(density / 100.0);
                    int generations = board.SimulateUntilStable();
                    generationsList.Add(generations);
                }
            }

            PlotGenerations(generationsList, densities);
        }

        public static void PlotGenerations(List<int> generationsList, List<int> densities)
        {
            int width = 800;
            int height = 600;
            Bitmap bitmap = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(bitmap);

            Pen pen = new Pen(Color.Black);
            Brush brush = new SolidBrush(Color.Black);
            Font font = new Font("Arial", 10);
            Brush axisBrush = new SolidBrush(Color.Red);

            int maxGenerations = generationsList.Max();
            int densityCount = densities.Count;
            int totalBars = densityCount * 10;
            int barWidth = width / totalBars;

            for (int i = 0; i < densityCount; i++)
            {
                int densityStartIndex = i * 10;
                for (int j = 0; j < 10; j++)
                {
                    int index = densityStartIndex + j;
                    int barHeight = (int)((double)generationsList[index] / maxGenerations * height);
                    g.FillRectangle(brush, (i * 10 + j) * barWidth, height - barHeight, barWidth, barHeight);
                }
            }

            // Add axes labels
            g.DrawString("Плотность заполнения (%)", font, axisBrush, new PointF(width / 2 - 60, height - 30));
            g.RotateTransform(-90);
            g.DrawString("Число поколений до стабильности", font, axisBrush, new PointF(-height / 2 - 80, 10));
            g.RotateTransform(90);

            bitmap.Save("plot.png");
        }
    }
}

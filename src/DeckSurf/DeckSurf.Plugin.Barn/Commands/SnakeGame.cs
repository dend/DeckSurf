using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace DeckSurf.Plugin.Barn.Commands
{
    class SnakeGame : IDeckSurfCommand
    {
        public string Name => "Snake Game";

        public string Description => "Plays a game of snake on the Stream Deck button grid.";

        private Queue<int> _snake;
        private SnakeDirection _direction;
        private int _head;
        private Timer _timer;
        private readonly object _lock = new();
        private int _columns = 8;
        private int _rows = 4;
        private byte[] _snakeImage;
        private byte[] _emptyImage;

        public SnakeGame()
        {
            _snake = new();
            _direction = SnakeDirection.RIGHT;
        }

        enum SnakeDirection
        {
            UP,
            DOWN,
            LEFT,
            RIGHT,
        }

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
            lock (_lock)
            {
                var headRow = _head / _columns;
                var pressedButtonRow = activatingButton / _columns;

                Debug.WriteLine(headRow);
                Debug.WriteLine(pressedButtonRow);

                if (headRow != pressedButtonRow)
                {
                    if (pressedButtonRow > headRow)
                    {
                        _direction = SnakeDirection.DOWN;
                    }
                    else
                    {
                        _direction = SnakeDirection.UP;
                    }
                }
                else
                {
                    if (activatingButton >= _head)
                    {
                        _direction = SnakeDirection.RIGHT;
                    }
                    else
                    {
                        _direction = SnakeDirection.LEFT;
                    }
                }
            }
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            _columns = mappedDevice.ButtonColumns;
            _rows = mappedDevice.ButtonRows;

            // SetKeyColor only works on the Stream Deck Neo, so the snake is
            // rendered with full key images, which work on every model.
            _snakeImage = ImageHelper.CreateBlankImage(mappedDevice.ButtonResolution, DeviceColor.White);
            _emptyImage = ImageHelper.CreateBlankImage(mappedDevice.ButtonResolution, DeviceColor.Black);

            // Initialize the snake to fit within the first row of the device.
            _snake.Clear();
            var initialLength = Math.Min(3, _columns);
            for (int i = 0; i < initialLength; i++)
            {
                _snake.Enqueue(i);
            }
            _head = initialLength - 1;

            mappedDevice.ClearButtons();

            UpdateSnakeRendering(mappedDevice);
            _timer = new Timer(1000);
            _timer.Elapsed += (s, e) =>
            {
                try
                {
                    lock (_lock)
                    {
                        var clearedIndex = UpdateSnakePosition(_direction);
                        mappedDevice.SetKey(clearedIndex, _emptyImage);
                        UpdateSnakeRendering(mappedDevice);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in snake game timer callback: {ex}");
                }
            };
            _timer.Start();
        }

        private int UpdateSnakePosition(SnakeDirection direction)
        {
            int col = _head % _columns;
            int row = _head / _columns;

            switch (direction)
            {
                case SnakeDirection.RIGHT:
                    {
                        if (col == _columns - 1)
                            _head = row * _columns;
                        else
                            _head++;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.LEFT:
                    {
                        if (col == 0)
                            _head = row * _columns + (_columns - 1);
                        else
                            _head--;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.DOWN:
                    {
                        if (row >= _rows - 1)
                            _head = col;
                        else
                            _head += _columns;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.UP:
                    {
                        if (row < 1)
                            _head = (_rows - 1) * _columns + col;
                        else
                            _head -= _columns;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
            }

            return -1;
        }

        private void UpdateSnakeRendering(IConnectedDevice mappedDevice)
        {
            foreach (var snakeNode in _snake)
            {
                mappedDevice.SetKey(snakeNode, _snakeImage);
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}

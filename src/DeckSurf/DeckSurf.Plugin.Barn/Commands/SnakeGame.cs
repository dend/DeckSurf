using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    [CompatibleWith(DeviceModel.XL2022)]
    [CompatibleWith(DeviceModel.Original)]
    [CompatibleWith(DeviceModel.Original2019)]
    [CompatibleWith(DeviceModel.MK2)]
    [CompatibleWith(DeviceModel.Mini)]
    [CompatibleWith(DeviceModel.Mini2022)]
    [CompatibleWith(DeviceModel.Plus)]
    [CompatibleWith(DeviceModel.Neo)]
    class SnakeGame : IDeckSurfCommand
    {
        public string Name => "Snake Game";

        public string Description => "A simple game of snake that can be played on Stream Deck.";

        private Queue<int> _snake;
        private SnakeDirection _direction;
        private int _head;
        private Timer _timer;
        private readonly object _lock = new();
        private int _columns = 8;
        private int _rows = 4;

        public SnakeGame()
        {
            _snake = new();
            _snake.Enqueue(0);
            _snake.Enqueue(1);
            _snake.Enqueue(2);
            _snake.Enqueue(3);
            _head = 3;

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
                        mappedDevice.SetKeyColor(clearedIndex, DeviceColor.Black);
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
                mappedDevice.SetKeyColor(snakeNode, DeviceColor.White);
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}

using Deck.Surf.SDK.Core;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace Deck.Surf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class SnakeGame : IDSCommand
    {
        public string Name => "Snake Game";

        public string Description => "A simple game of snake that can be played on Stream Deck.";

        private Queue<int> _snake;
        private SnakeDirection _direction;
        private int _head;

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

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice, int activatingButton = -1)
        {
            var headRow = _head / 8;
            var pressedButtonRow = activatingButton / 8;

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

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            mappedDevice.ClearPanel();

            UpdateSnakeRendering(mappedDevice);
            Timer timer = new(1000);
            timer.Elapsed += (s, e) =>
            {
                DeviceManager.SetKey(mappedDevice, UpdateSnakePosition(_direction), DeviceConstants.XLDefaultBlackButton);
                UpdateSnakeRendering(mappedDevice);
            };
            timer.Start();
        }

        private int UpdateSnakePosition(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.RIGHT:
                    {
                        _head++;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.LEFT:
                    {
                        _head--;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.DOWN:
                    {
                        _head += 8;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
                case SnakeDirection.UP:
                    {
                        _head -= 8;
                        _snake.Enqueue(_head);
                        return _snake.Dequeue();
                    }
            }

            return -1;
        }

        private void UpdateSnakeRendering(ConnectedDevice mappedDevice)
        {
            foreach (var snakeNode in _snake)
            {
                DeviceManager.SetKey(mappedDevice, snakeNode, DeviceConstants.XLDefaultWhiteButton);
            }
        }
    }
}

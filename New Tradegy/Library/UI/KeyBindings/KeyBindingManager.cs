using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI.KeyBindings
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public static class KeyBindingManager
    {
        private static readonly Dictionary<(Keys, bool, bool, bool), Action> _bindings = 
                            new Dictionary<(Keys, bool, bool, bool), Action>();

        // Main registration method
        public static void Register(Keys key, bool shift, bool ctrl, bool alt, Action action)
        {
            _bindings[(key, shift, ctrl, alt)] = action;
        }

        // Overload for char-based key registration (e.g., 'F', 'f')
        public static void Register(char c, bool ctrl, bool alt, Action action)
        {
            var (key, shift) = KeyHelper.FromChar(c);
            Register(key, shift, ctrl, alt, action);
        }

        public static void Register(char c, bool ctrl, bool alt, Func<Task> asyncAction)
        {
            // () => Task.Run(asyncAction) : internally wrap as Action Type
            // calling itself recursively, Register(c, ctrl, alt, () => Task.Run(asyncAction)); 
            Action wrapped = () => Task.Run(asyncAction);
            Register(c, ctrl, alt, wrapped);  // ✅ now calls the correct overload
        }

        // Try handling a key press event
        public static bool TryHandle(Keys keyData)
        {
            Keys keyOnly = keyData & ~Keys.Modifiers;
            bool isShift = (keyData & Keys.Shift) == Keys.Shift;
            bool isCtrl = (keyData & Keys.Control) == Keys.Control;
            bool isAlt = (keyData & Keys.Alt) == Keys.Alt;

            if (_bindings.TryGetValue((keyOnly, isShift, isCtrl, isAlt), out var action))
            {
                action?.Invoke();
                return true;
            }
            return false;
        }
    }

    public static class KeyHelper
    {
        public static int DealMoney(int current, char direction)
        {
            // Buying Money
            int[] steps = new[] { 0, 100, 500, 1000, 2000, 3500, 5000, 7000, 10000, 15000, 20000, 30000, 50000 };

            // Handle boundary conditions
            if (direction == '+')
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    if (current < steps[i])
                        return steps[i];
                }
                return steps[steps.Length - 1]; // Return last step if current >= max
            }
            else if (direction == '-')
            {
                for (int i = steps.Length - 1; i >= 0; i--)
                {
                    if (current > steps[i])
                        return steps[i];
                }
                return steps[0]; // Return first step if current <= min
            }

            // Invalid direction
            throw new ArgumentException("Direction must be '+' or '-'.");
        }

        public static (Keys key, bool shift) FromChar(char c)
        {
            if (char.IsLetter(c))
            {
                var upper = char.ToUpper(c);
                bool isShift = char.IsUpper(c);
                return ((Keys)(int)upper, isShift);
            }
            else if (char.IsDigit(c))
            {
                return ((Keys)(Keys.D0 + (c - '0')), false);
            }
            else
            {
                switch (c)
                {
                    case ' ': return (Keys.Space, false);

                    // 숫자키 윗줄 (` ~ 1! 2@ 3# 4$ 5% 6^ 7& 8* 9( 0) )
                    case '`': return (Keys.Oem3, false);
                    case '~': return (Keys.Oem3, true);

                    case '!': return (Keys.D1, true);
                    case '@': return (Keys.D2, true);
                    case '#': return (Keys.D3, true);
                    case '$': return (Keys.D4, true);
                    case '%': return (Keys.D5, true);
                    case '^': return (Keys.D6, true);
                    case '&': return (Keys.D7, true);
                    case '*': return (Keys.D8, true);
                    case '(': return (Keys.D9, true);
                    case ')': return (Keys.D0, true);

                    // 대괄호 / 역슬래시
                    case '[': return (Keys.OemOpenBrackets, false);
                    case ']': return (Keys.OemCloseBrackets, false);
                    case '\\': return (Keys.Oem5, false);

                    // 쉼표, 마침표, 슬래시, 플러스 등 필요한 것들
                    case ',': return (Keys.Oemcomma, false);
                    case '.': return (Keys.OemPeriod, false);
                    case '/': return (Keys.OemQuestion, false);   // 보통 / 키
                    case '-': return (Keys.OemMinus, false);
                    case '=': return (Keys.Oemplus, false);

                    // 필요하면 여기 계속 추가

                    default:
                        throw new ArgumentException($"Unsupported char '{c}' for key binding.");
                }
            }
        }
    }
}

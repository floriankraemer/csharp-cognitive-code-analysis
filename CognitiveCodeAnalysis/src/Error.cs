/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace CognitiveCodeAnalysis
{
    public class Error
    {
        // Method with high cognitive complexity - many parameters, nested conditionals, variables
        public void HighlyComplexMethod(string param1, int param2, bool param3, object param4, List<string> param5)
        {
            var result1 = "";
            var result2 = 0;
            var result3 = false;
            var result4 = new List<string>();
            var result5 = DateTime.Now;

            if (param1 != null)
            {
                if (param2 > 0)
                {
                    if (param3)
                    {
                        result1 = param1.ToUpper();
                        result2 = param2 * 2;

                        if (param4 != null)
                        {
                            result3 = true;
                            result4 = param5.Where(x => x.Length > 5).ToList();

                            if (result4.Count > 0)
                            {
                                foreach (var item in result4)
                                {
                                    result5 = result5.AddDays(1);
                                    Console.WriteLine(item);
                                }
                            }
                            else
                            {
                                result5 = DateTime.MinValue;
                            }
                        }
                        else
                        {
                            result3 = false;
                        }
                    }
                    else
                    {
                        result1 = param1.ToLower();
                        result2 = param2 / 2;
                    }
                }
                else
                {
                    result1 = "default";
                    result2 = 0;
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(param1));
            }

            // Multiple returns
            if (result1.Length > 10)
                return;

            if (result2 < 0)
                return;

            if (result3)
                return;
        }

        // Method with many lines of code and complex logic
        public int VeryLongMethod(int a, int b, int c, int d, int e)
        {
            var total = 0;
            var counter = 0;
            var flag = false;
            var list = new List<int>();
            var dict = new Dictionary<int, string>();

            // Lots of lines with repetitive but complex logic
            for (int i = 0; i < a; i++)
            {
                if (i % 2 == 0)
                {
                    total += i;
                    counter++;

                    if (counter > 10)
                    {
                        flag = true;
                        list.Add(i);

                        if (list.Count > 5)
                        {
                            dict[i] = i.ToString();

                            if (dict.Count > 3)
                            {
                                total *= 2;
                            }
                        }
                    }
                    else
                    {
                        flag = false;
                    }
                }
                else
                {
                    total -= i;
                    counter--;

                    if (counter < 0)
                    {
                        flag = !flag;
                    }
                }
            }

            // More complex nested logic
            if (b > 0)
            {
                for (int j = 0; j < b; j++)
                {
                    if (j % 3 == 0)
                    {
                        total += j;
                        if (total > 100)
                        {
                            return total;
                        }
                    }
                }
            }

            if (c > 0)
            {
                while (c > 0)
                {
                    total += c;
                    c--;

                    if (c % 5 == 0)
                    {
                        break;
                    }
                }
            }

            // Even more logic
            switch (d)
            {
                case 1:
                    total += 10;
                    break;
                case 2:
                    total += 20;
                    if (e > 0)
                    {
                        total += e;
                    }
                    break;
                case 3:
                    total += 30;
                    for (int k = 0; k < e; k++)
                    {
                        total += k;
                    }
                    break;
                default:
                    total += 1;
                    break;
            }

            return total;
        }

        // Method with deep nesting and multiple conditionals
        public bool DeeplyNestedMethod(int x, int y, int z, string text, bool condition)
        {
            var result = false;

            if (x > 0)
            {
                if (y > 0)
                {
                    if (z > 0)
                    {
                        if (text != null)
                        {
                            if (text.Length > 0)
                            {
                                if (condition)
                                {
                                    if (x + y + z > 10)
                                    {
                                        result = true;
                                    }
                                    else
                                    {
                                        result = false;
                                    }
                                }
                                else
                                {
                                    if (text.Contains("test"))
                                    {
                                        result = true;
                                    }
                                    else
                                    {
                                        result = false;
                                    }
                                }
                            }
                            else
                            {
                                result = false;
                            }
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    result = false;
                }
            }
            else
            {
                result = false;
            }

            return result;
        }

        // Simple method that should not trigger warnings
        public void SimpleMethod()
        {
            Console.WriteLine("Hello World");
        }
    }
}

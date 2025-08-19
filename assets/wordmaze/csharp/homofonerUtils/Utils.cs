// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

using System.Diagnostics;

namespace Main;

public static class Utils
{
    public static float DemoNode(float a, float b)
    {
        return a + b;
    }

    public static IEnumerable<IEnumerable<IEnumerable<bool>>> TransistionsToCells(IEnumerable<IEnumerable<IEnumerable<int>>> transitions, int gridSize)
    {
        var output = new List<List<bool[]>>();
        for(int x = 0; x < gridSize; x++)
        {
            var layer1 = new List<bool[]>();
            for (int y = 0; y < gridSize; y++)
            {
                IEnumerable<int> downright = GetModulod(transitions, x, y);
                bool[] inwards =
                {
                    downright.ElementAt(0) == 1,
                    downright.ElementAt(1) == 1,
                    GetModulod(transitions, x - 1, y).ElementAt(0) == 0,
                    GetModulod(transitions, x, y - 1).ElementAt(1) == 0,
                };
                layer1.Add(inwards);
            }
            output.Add(layer1);
        }
        return output;
    }

    public static IEnumerable<IEnumerable<int>> IsTraversable(IEnumerable<IEnumerable<IEnumerable<bool>>> transitions)
    {
        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };
        int size = transitions.Count();
        var output = new List<List<int>>();
        for (int x = 0; x < size; x++)
        {
            var col = new List<int>();
            for (int y = 0; y < size; y++)
            {
                bool[,] cells = new bool[size, size];
                bool hasExpanded = false;
                cells[x,y] = true;
                int n = 0;
                while (true) {
                    n++;
                    for (int x2 = 0; x2 < size; x2++)
                    {
                        for (int y2 = 0; y2 < size; y2++)
                        {
                            /*string debug = "";
                            for (int a = 0; a < size; a++)
                            {
                                for (int b = 0; b < size; b++)
                                {
                                    if (cells[a,b])
                                    {
                                        debug += "#";
                                    }
                                    else
                                    {
                                        debug += ".";
                                    }
                                }
                                debug += "\n";
                            }
                            Debug.WriteLine(debug);*/
                            if (!cells[x2, y2])
                            {
                                continue;
                            }
                            for (int i = 0; i < 4; i++)
                            {
                                if (!transitions.ElementAt(x2).ElementAt(y2).ElementAt(i))
                                {
                                    if (cells[(x2 + dx[i] + size) % size, (y2 + dy[i] + size) % size] == false)
                                    {
                                        cells[(x2 + dx[i] + size) % size, (y2 + dy[i] + size) % size] = true;
                                        hasExpanded = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!hasExpanded)
                    {
                        bool hasEmpty = false;
                        int count = 0;
                        for (int a = 0; a < size; a++)
                        {
                            for (int b = 0; b < size; b++)
                            {
                                if (!cells[a, b])
                                {
                                    hasEmpty = true;
                                }
                                else
                                {
                                    count++;
                                }
                            }
                        }
                        col.Add(count);
                        /*if (hasEmpty)
                        {
                            col.Add(0);
                        }
                        else
                        {
                            col.Add(1);
                        }*/
                        break;
                    }
                    hasExpanded = false;
                    if (n > 100)
                    {
                        col.Add(-1);
                        break;
                    }
                }
            }
            output.Add(col);
        }
        return output;
    }

    public static IEnumerable<IEnumerable<IEnumerable<int>>> ReverseRandomRing(IEnumerable<IEnumerable<IEnumerable<int>>> transitions)
    {
        int size = transitions.Count();
        var rand = new Random();
        bool[][][] cellTransitions = IEnumerableToJaggedArray(TransistionsToCells(transitions, size));
        List<int[]> path = new List<int[]>();
        List<int> pathdir = new List<int>();
        path.Add([rand.Next(0, size), rand.Next(0, size)]);
        Debug.WriteLine($"Start: {path.Last()[0]}, {path.Last()[1]}");
        for (int n = 0; n < 1000; n++)
        {
            int[] pos = path.Last();

            int dir;
            do
            {
                dir = rand.Next(0, 4);
                n++;
            } while (!cellTransitions[pos[0]][pos[1]][dir] && n < 1000);

            int x = (pos[0] + (dir == 0 ? 1 : (dir == 2 ? -1 : 0)) + size) % size;
            int y = (pos[1] + (dir == 1 ? 1 : (dir == 3 ? -1 : 0)) + size) % size;
            
            Debug.WriteLine($"Step {n}: {x}, {y}, dir: {dir}");

            int idx = path.FindIndex(0, path.Count, p => p[0] == x && p[1] == y);
            pathdir.Add(dir);
            if (idx == -1)
            {
                path.Add([x, y]);
            }
            else
            {
                path = path.Skip(idx).ToList();
                pathdir = pathdir.Skip(idx).ToList();
                break;
            }
            
        }

        string pathdisplay = "";
        for(int x = 0; x < size; x++)
        {
            for(int y = 0; y < size; y++)
            {
                int idx = path.FindIndex(p => p[0] == y && p[1] == x);
                if (idx != -1)
                {
                    switch(pathdir[idx])
                    {
                        case 0:
                            pathdisplay += ">";
                            break;
                        case 1:
                            pathdisplay += "v";
                            break;
                        case 2:
                            pathdisplay += "<";
                            break;
                        case 3:
                            pathdisplay += "^";
                            break;
                    }
                }
                else
                {
                    pathdisplay += ".";
                }
            }
            pathdisplay += "\n";
        }

        Debug.WriteLine(pathdisplay);

        Debug.WriteLine($"path length: {path.Count}");

        int[][][] transitionsArray = IEnumerableToJaggedArray(transitions);

        for(int i = 0; i < path.Count; i++)
        {
            int[] pos = path[i];
            int dir = pathdir[i];

            int x = pos[0];
            int y = pos[1];

            Debug.WriteLine($"Toggling: {x}, {y}, dir: {dir}");

            if (dir == 2)
            {
                x = (x - 1 + size) % size;
                dir = 0;
            }else if(dir == 3)
            {
                y = (y - 1 + size) % size;
                dir = 1;
            }

            transitionsArray[x][y][dir] = 1 - transitionsArray[x][y][dir];

        }
        return transitionsArray;
    }

    private static T[][][] IEnumerableToJaggedArray<T>(IEnumerable<IEnumerable<IEnumerable<T>>> data)
    {
        return data.Select(plane => plane
            .Select(row => row.ToArray())
            .ToArray()
        ).ToArray();
    }

    private static T GetModulod<T>(IEnumerable<IEnumerable<T>> data, int x, int y)
    {
        int len = data.Count();
        return data.ElementAt((x + len) % len).ElementAt((y + len) % len);
    }
}
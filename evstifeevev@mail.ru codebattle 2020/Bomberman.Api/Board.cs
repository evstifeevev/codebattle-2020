/*-
 * #%L
 * Codenjoy - it's a dojo-like platform from developers to developers.
 * %%
 * Copyright (C) 2018 Codenjoy
 * %%
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public
 * License along with this program.  If not, see
 * <http://www.gnu.org/licenses/gpl-3.0.html>.
 * #L%
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bomberman.Api
{
    public class Board
    {
        // v 0.642
        #region Added fields

        // "Simplest" means the worst scenario

        // Prices 
        public static int PriceWall = 30;
        public static int PriceMC = 200;
        public static int PricePlayer = 600;
        public static int PriceBombPlayer = 1200;
        public static int DeathPenalty = -700;

        // Current target
        public static Point currentGoal = new Point();

        // Contains no more than 2 nodes, the first one 
        // is next move
        public static Stack<Point> Path = new Stack<Point>();

        // Contains all path nodes
        public static Dictionary<Point, float> PathAllNodes = new Dictionary<Point, float>();

        // Danger mark of points
        public static Dictionary<Point, float> pointsDanger = new Dictionary<Point, float>();

        // Elements on the map
        public static Dictionary<Point, Element> mapElements = new Dictionary<Point, Element>();

        // Bombs of mine
        public static Dictionary<Point, int> MineBombs = new Dictionary<Point, int>();

        // Objects going to blow on the next move
        public static List<Point> GoingToBlow = new List<Point>();

        // Bombs going to blow on the next move in case 
        // every other bomberman plants bomb
        public static List<Point> ChainBombs = new List<Point>();

        // I plant the bomb
        public static List<Point> ChainBombsAct = new List<Point>();

        // I plant the bomb after I move
        public static List<Point> ChainBombsAct2 = new List<Point>();

        public static List<Point> OtherBombermans = new List<Point>();

        // Points going to blast on the next move by:
        // ChainBombs
        public static List<Point> FutureBlast = new List<Point>();

        // ChainBombsAct
        public static List<Point> FutureBlastAct = new List<Point>();

        // ChainBombsAct2
        public static List<Point> FutureBlastAct2 = new List<Point>();

        // Point of the average profit location
        public Point GlobalGoal = new Point();

        // Point of the most valuable target near me
        public Point LocalGoal = new Point();

        // Profit of me planting the bomb before movement
        public int ProfitAct1 = -50000;

        // Profit of me planting after the movement
        public int ProfitAct2 = -50000;

        public Point MyLocation = new Point();
        #endregion
        private String BoardString { get; }

        private LengthToXY LengthXY;

        public Board(String boardString)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            BoardString = boardString.Replace("\n", "");

            LengthXY = new LengthToXY(BoardSize);

            // Create map of elements
            for (int x = 0; x <= BoardSize; x++)
            {
                for (int y = 0; y <= BoardSize; y++)
                {
                    var point = new Point(x, y);
                    mapElements[point] = GetAtFirst(point);
                }
            }

            // Additional initializations
            CalculateChainBombs();
            CalculateChainBombsCountMe();
            CalculateChainBombsCountMe2();

            SetFutureBlastSimplest();
            SetFutureBlastSimplestCountMe();
            SetFutureBlastSimplestCountMe2();

            OtherBombermans = GetOtherBombermans();

            MyLocation = GetBombermanFirst();

            var me = GetBomberman();

            var newMineBombs = new Dictionary<Point, int>();
            foreach (var b in MineBombs.Keys)
            {
                newMineBombs.Add(b, MineBombs[b]);
                newMineBombs[b]--;
            }
            if (IsAt(me, Element.BOMB_BOMBERMAN) && !newMineBombs.ContainsKey(me))
            {
                newMineBombs.Add(me, 4);
            }

            MineBombs = new Dictionary<Point, int>();
            foreach (var b in newMineBombs.Keys)
            {
                if (newMineBombs[b] > 0)
                {
                    MineBombs.Add(b, newMineBombs[b]);
                }

            }

            // setting up the targets
            GlobalGoal = GetGlobalGoal();
            LocalGoal = GetLocalGoalSimplest();

            calculateDanger();

            // Update mineBombs
            // evaluate points going to blow by my bombs
            AfterAct(false);

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
        }

        static Board()
        {

        }

        /// <summary>
        /// GameBoard size (actual board size is Size x Size cells)
        /// </summary>
        public int BoardSize
        {
            get
            {
                return (int)Math.Sqrt(BoardString.Length);
            }
        }

        public void calculateDanger()
        {
            pointsDanger = new Dictionary<Point, float>();
            foreach (var m in mapElements.Keys)
            {
                float score = 0;
                score += 10 * (float)GetDistance(m, LocalGoal);
                score += 10 * (float)GetDistance(m, GlobalGoal);
                if (FutureBlastAct.Contains(m))
                {
                    score += 10;
                }
                if (FutureBlast.Contains(m))
                {
                    score += 10000;
                }
                else if (IsAt(m, Element.WALL))
                {
                    score += 100000;
                }
                else if (IsAnyOfAt(m, Element.OTHER_BOMBERMAN, Element.OTHER_BOMB_BOMBERMAN, Element.MEAT_CHOPPER, Element.DESTROYABLE_WALL))
                {
                    if (!GoingToBlow.Contains(m))
                    {
                        if (IsAt(m, Element.OTHER_BOMBERMAN))
                        {
                            score -= PricePlayer;
                        }
                        else if (IsAt(m, Element.OTHER_BOMB_BOMBERMAN))
                        {
                            score -= PriceBombPlayer;
                        }
                        else if (IsAt(m, Element.MEAT_CHOPPER))
                        {
                            // Meat chopper is still a threat
                            score += 1;
                            //score -= PriceMC;
                        }
                        else
                        {
                            score -= PriceWall;
                        }
                    }
                    else
                    {
                        score += 100;
                    }
                }
                else
                {

                }
                pointsDanger.Add(m, score);
            }
        }

        public Point GetBombermanFirst()
        {
            var bomberman = Get(Element.BOMBERMAN);
            if (bomberman.Count == 1)
            {
                return bomberman[0];
            }
            else
            {
                var bombBomberman = Get(Element.BOMB_BOMBERMAN);
                if (bombBomberman.Count == 1)
                {
                    return bombBomberman[0];
                }
            }
            return Get(Element.DEAD_BOMBERMAN)[0];
        }
        public Point GetBomberman()
        {
            return MyLocation;
        }

        public List<Point> GetOtherBombermans()
        {
            return Get(Element.OTHER_BOMBERMAN)
                .Concat(Get(Element.OTHER_BOMB_BOMBERMAN))
                .ToList();
        }

        public Element GetAtFirst(Point point)
        {
            if (point.IsOutOf(BoardSize))
            {
                return Element.WALL;
            }
            return (Element)BoardString[LengthXY.GetLength(point.X, point.Y)];
        }

        public Element GetAt(Point point)
        {
            if (mapElements.ContainsKey(point))
            {
                return mapElements[point];
            }
            return GetAtFirst(point);
        }

        public bool IsAt(Point point, Element element)
        {
            if (mapElements.ContainsKey(point))
            {
                return mapElements[point] == element;
            }
            if (point.IsOutOf(BoardSize))
            {
                return false;
            }
            return mapElements[point] == element;
            //return GetAt(point) == element;
        }

        public string BoardAsString()
        {
            string result = "";
            for (int i = 0; i < BoardSize; i++)
            {
                result += BoardString.Substring(i * BoardSize, BoardSize);
                result += "\n";
            }
            return result;
        }

        /// <summary>
        /// gets board view as string
        /// </summary>
        public string ToString()
        {
            return string.Format("{0}\n" +
                     "Bomberman at: {1}\n" +
                     "Other bombermans at: {2}\n" +
                     "Meat choppers at: {3}\n" +
                     "Destroy walls at: {4}\n" +
                     "Bombs at: {5}\n" +
                     "Blasts: {6}\n" +
                     "Expected blasts at: {7}",
                     BoardAsString(),
                     GetBomberman(),
                     ListToString(GetOtherBombermans()),
                     ListToString(GetMeatChoppers()),
                     ListToString(GetDestroyableWalls()),
                     ListToString(GetBombs()),
                     ListToString(GetBlasts()),
                     ListToString(GetFutureBlasts()));
        }

        private string ListToString(List<Point> list)
        {
            return string.Join(",", list.ToArray());
        }

        public List<Point> GetBarriers()
        {
            return GetMeatChoppers()
                .Concat(GetWalls())
                .Concat(GetBombs())
                .Concat(GetDestroyableWalls())
                .Concat(GetOtherBombermans())
                .Distinct()
                .ToList();
        }

        public List<Point> GetMeatChoppers()
        {
            return Get(Element.MEAT_CHOPPER);
        }

        public List<Point> Get(Element element)
        {
            List<Point> result = new List<Point>();
            if (mapElements.Count > 0)
            {
                foreach (var p in mapElements)
                {
                    if (p.Value == element)
                    {
                        result.Add(p.Key);
                    }
                }
                return result;
            }


            for (int i = 0; i < BoardSize * BoardSize; i++)
            {
                Point pt = LengthXY.GetXY(i);

                if (IsAt(pt, element))
                {
                    result.Add(pt);
                }
            }

            return result;
        }

        public List<Point> GetWalls()
        {
            return Get(Element.WALL);
        }

        public List<Point> GetDestroyableWalls()
        {
            return Get(Element.DESTROYABLE_WALL);
        }

        public List<Point> GetBombs()
        {
            return Get(Element.BOMB_TIMER_1)
                .Concat(Get(Element.BOMB_TIMER_2))
                .Concat(Get(Element.BOMB_TIMER_3))
                .Concat(Get(Element.BOMB_TIMER_4))
                .Concat(Get(Element.BOMB_TIMER_5))
                .Concat(Get(Element.BOMB_BOMBERMAN))
                .Concat(Get(Element.OTHER_BOMB_BOMBERMAN))
                .ToList();
        }

        public List<Point> GetBombsTimer1()
        {
            return Get(Element.BOMB_TIMER_1)
                .ToList();
        }

        public List<Point> GetBombsExceptTimer1()
        {
            return Get(Element.BOMB_TIMER_2)
                .Concat(Get(Element.BOMB_TIMER_3))
                .Concat(Get(Element.BOMB_TIMER_4))
                .Concat(Get(Element.BOMB_TIMER_5))
                .Concat(Get(Element.BOMB_BOMBERMAN))
                .Concat(Get(Element.OTHER_BOMB_BOMBERMAN))
                .ToList();
        }

        public List<Point> GetBombsExceptTimer12()
        {
            return Get(Element.BOMB_TIMER_3)
                .Concat(Get(Element.BOMB_TIMER_4))
                .Concat(Get(Element.BOMB_TIMER_5))
                .Concat(Get(Element.BOMB_BOMBERMAN))
                .Concat(Get(Element.OTHER_BOMB_BOMBERMAN))
                .ToList();
        }

        public List<Point> GetBlasts()
        {
            return Get(Element.BOOM);
        }

        /// <summary>
        /// Interesting, but not very useful.
        /// </summary>
        /// <returns></returns>
        public List<Point> GetFutureBlasts()
        {
            var bombs = GetBombs();
            var result = new List<Point>();
            foreach (var bomb in bombs)
            {
                result.Add(bomb);
                // only 1 cell, seriously? The blast radius is not less than 3...
                result.Add(bomb.ShiftLeft());
                result.Add(bomb.ShiftRight());
                result.Add(bomb.ShiftTop());
                result.Add(bomb.ShiftBottom());
            }

            return result.Where(blast => !blast.IsOutOf(BoardSize) && !GetWalls().Contains(blast)).Distinct().ToList();
        }

        public bool IsAnyOfAt(Point point, params Element[] elements)
        {
            if (mapElements.Count > 0 && mapElements.ContainsKey(point))
            {
                foreach (var e in elements)
                {
                    if (mapElements[point] == e)
                    {
                        return true;
                    }
                }
                return false;
            }
            return elements.Any(elem => IsAt(point, elem));
        }

        public bool IsAnyOfAt(Point point, IEnumerable<Element> elements)
        {
            return IsAnyOfAt(point, elements.ToArray());
        }

        public bool IsNear(Point point, Element element)
        {
            if (point.IsOutOf(BoardSize))
                return false;

            return IsAt(point.ShiftLeft(), element) ||
                   IsAt(point.ShiftRight(), element) ||
                   IsAt(point.ShiftTop(), element) ||
                   IsAt(point.ShiftBottom(), element);
        }

        public bool IsBarrierAt(Point point)
        {
            return GetBarriers().Contains(point);
        }

        public List<Point> BariersBetween(Point p1, Point p2)
        {
            var result = new List<Point>();
            // return false if p1 is the same point as p2
            // or their lines dont cross
            // or they are next to each other
            if ((p1.X == p2.X && p1.Y == p2.Y)
                || (p1.X != p2.X && p1.Y != p2.Y)
                || (p1.Y == p2.Y && (Math.Abs(p1.X - p2.X) == 1))
                || (p1.X == p2.X && Math.Abs(p1.Y - p2.Y) == 1))
            {
                // return empty list
                return result;
            }

            if (p1.X == p2.X)
            {
                if (p1.Y > p2.Y)
                {
                    for (int y = p1.Y - 1; y > p2.Y + 1; y--)
                    {
                        if (IsBarrierAt(new Point(p1.X, y)))
                        {
                            result.Add(new Point(p1.X, y));
                        }
                    }
                }
                else
                {
                    for (int y = p1.Y + 1; y < p2.Y - 1; y++)
                    {
                        if (IsBarrierAt(new Point(p1.X, y)))
                        {
                            result.Add(new Point(p1.X, y));
                        }
                    }
                }
            }
            else if (p1.Y == p2.Y)
            {
                int xMin = p1.X > p2.X ? p2.X : p1.X;
                int xMax = p1.X < p2.X ? p2.X : p1.X;
                for (int x = xMin + 1; x < xMax - 1; x++)
                {
                    if (IsBarrierAt(new Point(x, p1.Y)))
                    {
                        result.Add(new Point(x, p1.Y));
                    }
                }
            }

            return result;
        }

        public Element[] BombElements = new Element[]
        {
            Element.BOMB_TIMER_5, Element.BOMB_TIMER_4, Element.BOMB_TIMER_3,
            Element.BOMB_TIMER_2, Element.BOMB_TIMER_1, Element.BOMB_BOMBERMAN,
            Element.OTHER_BOMB_BOMBERMAN
        };


        public void SetFutureBlastSimplest()
        {
            var chainBombs = ChainBombs;
            var result = new List<Point>();

            var near = new List<Point>();

            // No chain bombs
            foreach (var bomb in chainBombs)
            {
                var blastNear = GetBlastNearPointSimplest(bomb);

                foreach (var point in blastNear)
                {
                    if (!result.Contains(point))
                    {
                        result.Add(point);

                    }
                }
            }
            FutureBlast = result;
        }

        public void SetFutureBlastSimplestCountMe()
        {
            var me = GetBomberman();
            var chainBombs = ChainBombsAct;
            //chainBombs.Add(me);
            var result = new List<Point>();
            // No chain bombs
            foreach (var bomb in chainBombs)
            {
                var blastNear = GetBlastNearPointSimplest(bomb);

                foreach (var point in blastNear)
                {
                    if (!result.Contains(point))
                    {
                        result.Add(point);

                    }
                }
            }
            FutureBlastAct = result;
        }

        public void SetFutureBlastSimplestCountMe2()
        {
            var chainBombs = ChainBombsAct2;
            var result = new List<Point>();
            // No chain bombs
            foreach (var bomb in chainBombs)
            {
                var blastNear = GetBlastNearPointSimplest(bomb);

                foreach (var point in blastNear)
                {
                    if (!result.Contains(point))
                    {
                        result.Add(point);

                    }
                }
            }
            FutureBlastAct2 = result;
        }

        public List<Point> GetChainBombsSimplest()
        {
            bool PlayersMove = false;
            bool PlayersPlant = true;

            var candidates = new Queue<Point>();

            var result = new List<Point>();

            // sources of the blast

            result = Get(Element.BOMB_TIMER_1)
     .Concat(Get(Element.BOMB_TIMER_2))
     .ToList();

            foreach (var b in MineBombs.Keys)
            {
                if (MineBombs[b] < 4)
                {
                    result.Add(b);
                }
            }

            foreach (var p in result)
            {
                candidates.Enqueue(p);
            }

            //100% possible chain bombs
            Queue<Point> otherBombs = new Queue<Point>();



            foreach (var p in GetBombsExceptTimer12())
            {
                otherBombs.Enqueue(p);
            }

            //  Possibile chain bombs
            if (PlayersPlant)
            {
                otherBombs.Concat(GetOtherBombermans());
            }

            int resultIndex = 0;


            for (resultIndex = 0; resultIndex < candidates.Count; resultIndex++)
            {
                var bomb = candidates.Dequeue();
                int otherBombIndex = 0;
                for (otherBombIndex = 0; otherBombIndex < otherBombs.Count; otherBombIndex++)
                {
                    var otherBomb = otherBombs.Dequeue();
                    if (IsOnBlastLine(bomb, otherBomb))
                    {
                        var obstacles = BariersBetween(bomb, otherBomb);
                        if (!PlayersMove)
                        {
                            // no obstacles, therefore the both with explode 
                            if (obstacles.Count == 0)
                            {
                                if (!result.Contains(otherBomb))
                                {
                                    candidates.Enqueue(otherBomb);
                                    otherBombIndex--;
                                    result.Add(otherBomb);

                                }
                            }
                            else
                            {
                                foreach (var obstacle in obstacles)
                                {
                                    var element = GetAt(obstacle);

                                    if (IsBombAt(obstacle, true))
                                    {
                                        if (!result.Contains(obstacle))
                                        {
                                            candidates.Enqueue(obstacle);
                                            otherBombIndex--;
                                            result.Add(obstacle);

                                        }
                                    }
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                }
            }
            return result;
        }

        // It is simplier to create second the same method with 
        // additional condition than use 1 supermethod and
        // pass parameters to it. Also it is much faster 

        public List<Point> GetChainBombsSimplestCountMe()
        {
            var me = GetBomberman();
            bool PlayersMove = false;
            bool PlayersPlant = true;

            var candidates = new Queue<Point>();

            var result = new List<Point>();

            // sources of the blast

            result = Get(Element.BOMB_TIMER_1)
     .Concat(Get(Element.BOMB_TIMER_2))
     .ToList();

            foreach (var b in MineBombs.Keys)
            {
                if (MineBombs[b] < 4)
                {
                    result.Add(b);
                }
            }
            foreach (var p in result)
            {
                candidates.Enqueue(p);
            }

            // 100% possible chain bombs
            Queue<Point> otherBombs = new Queue<Point>();

            foreach (var p in GetBombsExceptTimer1())
            {
                otherBombs.Enqueue(p);
            }

            otherBombs.Enqueue(me);

            //  Possibile chain bombs
            if (PlayersPlant)
            {
                otherBombs.Concat(GetOtherBombermans());
            }

            int resultIndex = 0;


            for (resultIndex = 0; resultIndex < candidates.Count; resultIndex++)
            {
                var bomb = candidates.Dequeue();
                int otherBombIndex = 0;
                for (otherBombIndex = 0; otherBombIndex < otherBombs.Count; otherBombIndex++)
                {
                    var otherBomb = otherBombs.Dequeue();
                    if (IsOnBlastLine(bomb, otherBomb))
                    {
                        var obstacles = BariersBetween(bomb, otherBomb);
                        if (!PlayersMove)
                        {
                            // no obstacles, therefore the both with explode 
                            if (obstacles.Count == 0)
                            {
                                if (!result.Contains(otherBomb))
                                {
                                    candidates.Enqueue(otherBomb);
                                    otherBombIndex--;
                                    result.Add(otherBomb);

                                }
                            }
                            else
                            {
                                foreach (var obstacle in obstacles)
                                {
                                    var element = GetAt(obstacle);

                                    if (IsBombAt(obstacle, true))
                                    {
                                        if (!result.Contains(obstacle))
                                        {
                                            candidates.Enqueue(obstacle);
                                            otherBombIndex--;
                                            result.Add(obstacle);

                                        }
                                    }
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                }
            }
            return result;
        }

        public List<Point> GetChainBombsSimplestCountMe2()
        {
            var me = GetBomberman();

            bool PlayersMove = false;
            bool PlayersPlant = true;

            var candidates = new Queue<Point>();

            var result = new List<Point>();

            // sources of the blast

            result = Get(Element.BOMB_TIMER_1);


            foreach (var b in MineBombs.Keys)
            {
                if (MineBombs[b] < 4)
                {
                    result.Add(b);
                }
            }

            foreach (var p in result)
            {
                candidates.Enqueue(p);
            }



            // possible chain bombs
            Queue<Point> otherBombs = new Queue<Point>();

            var aroundMe = GetSafePointsNear(me);
            if (aroundMe.Count == 0)
            {
                return ChainBombs;
            }
            else
            {
                foreach (var p in aroundMe)
                {
                    otherBombs.Enqueue(p);
                }
            }

            foreach (var p in GetBombsExceptTimer12())
            {
                otherBombs.Enqueue(p);
            }

            //  Possibile chain bombs
            if (PlayersPlant)
            {
                otherBombs.Concat(GetOtherBombermans());
            }

            int resultIndex = 0;


            for (resultIndex = 0; resultIndex < candidates.Count; resultIndex++)
            {
                var bomb = candidates.Dequeue();
                int otherBombIndex = 0;
                for (otherBombIndex = 0; otherBombIndex < otherBombs.Count; otherBombIndex++)
                {
                    var otherBomb = otherBombs.Dequeue();
                    if (IsOnBlastLine(bomb, otherBomb))
                    {
                        var obstacles = BariersBetween(bomb, otherBomb);
                        if (!PlayersMove)
                        {
                            // no obstacles, therefore the both with explode 
                            if (obstacles.Count == 0)
                            {
                                if (!result.Contains(otherBomb))
                                {
                                    candidates.Enqueue(otherBomb);
                                    otherBombIndex--;
                                    result.Add(otherBomb);

                                }
                            }
                            else
                            {
                                foreach (var obstacle in obstacles)
                                {
                                    var element = GetAt(obstacle);

                                    if (IsBombAt(obstacle, true))
                                    {
                                        if (!result.Contains(obstacle))
                                        {
                                            candidates.Enqueue(obstacle);
                                            otherBombIndex--;
                                            result.Add(obstacle);

                                        }
                                    }
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                }
            }
            return result;
        }

        public void CalculateChainBombs()
        {
            ChainBombs = GetChainBombsSimplest();
        }

        public void CalculateChainBombsCountMe()
        {
            //ChainBombs = GetChainBombsSimplest();
            ChainBombs = GetChainBombsSimplestCountMe();

        }
        public void CalculateChainBombsCountMe2()
        {
            ChainBombsAct2 = GetChainBombsSimplestCountMe2();
        }



        public bool IsBombAt(Point p, bool doesPlayerCount = false)
        {
            if (doesPlayerCount)
            {
                return IsAnyOfAt(p, Element.OTHER_BOMB_BOMBERMAN,
                Element.BOMB_TIMER_1, Element.BOMB_TIMER_2, Element.BOMB_TIMER_3,
                Element.BOMB_TIMER_4, Element.BOMB_TIMER_5, Element.BOMB_BOMBERMAN,
                Element.OTHER_BOMBERMAN);
            }
            // not sure about bomb_bomberman
            return IsAnyOfAt(p, Element.OTHER_BOMB_BOMBERMAN,
                Element.BOMB_TIMER_1, Element.BOMB_TIMER_2, Element.BOMB_TIMER_3,
                Element.BOMB_TIMER_4, Element.BOMB_TIMER_5, Element.BOMB_BOMBERMAN);
        }

        /// <summary>
        /// Checks if first point can blow the second
        /// ignoring bariers between them. 
        /// Simple but not accurate.
        /// </summary>
        public bool IsOnBlastLine(Point p1, Point p2)
        {
            return (p1.X == p2.X && Math.Abs(p1.Y - p2.Y) <= 2)
                || (p1.Y == p2.Y && Math.Abs(p1.X - p2.X) <= 2);
        }

        public static Element[] NotBariesSimplestElements = new Element[]
        {
                Element.BOMBERMAN,Element.BOMB_BOMBERMAN, Element.BOOM, Element.DeadMeatChopper,
                    Element.DEAD_BOMBERMAN, Element.DestroyedWall, Element.OTHER_BOMBERMAN,
                    Element.OTHER_BOMB_BOMBERMAN,Element.OTHER_DEAD_BOMBERMAN,
                    Element.MEAT_CHOPPER,Element.DeadMeatChopper,Element.Space,
        };

        public static Element[] BarierSimplestElements = new Element[]
        {
                Element.BOMB_TIMER_1,Element.BOMB_TIMER_2,Element.BOMB_TIMER_3,
                Element.BOMB_TIMER_4,Element.BOMB_TIMER_5,Element.WALL,Element.DESTROYABLE_WALL
        };

        public List<Point> GetBlastNearPointSimplest(Point p)
        {
            var result = new List<Point>();

            // Calculating the boundaries
            int xMin = p.X < 3 ? 0 : p.X - 3;
            int xMax = p.X > BoardSize - 3 ? BoardSize : p.X + 3;
            int yMin = p.Y < 3 ? 0 : p.Y - 3;
            int yMax = p.Y > BoardSize - 3 ? BoardSize : p.Y + 3;

            // Checking to the left of the point
            for (int x = p.X - 1; x >= xMin; x--)
            {
                Point checkable = new Point(x, p.Y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    if (IsAt(checkable, Element.DESTROYABLE_WALL))
                    {
                        result.Add(checkable);
                    }
                    break;
                }
            }

            // Checking to the right of the point
            for (int x = p.X + 1; x <= xMax; x++)
            {
                Point checkable = new Point(x, p.Y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the bottom of the point
            for (int y = p.Y - 1; y >= yMin; y--)
            {
                Point checkable = new Point(p.X, y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the top of the point
            for (int y = p.Y + 1; y <= yMax; y++)
            {
                Point checkable = new Point(p.X, y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        public bool IsGoingToBlowAct(Point p, Point p2)
        {
            var result = new List<Point>();

            // Calculating the boundaries
            int xMin = p.X < 3 ? 0 : p.X - 3;
            int xMax = p.X > BoardSize - 3 ? BoardSize : p.X + 3;
            int yMin = p.Y < 3 ? 0 : p.Y - 3;
            int yMax = p.Y > BoardSize - 3 ? BoardSize : p.Y + 3;

            // Checking to the left of the point
            for (int x = p.X - 1; x >= xMin; x--)
            {
                Point checkable = new Point(x, p.Y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    if (IsAt(checkable, Element.DESTROYABLE_WALL))
                    {
                        result.Add(checkable);
                    }
                    break;
                }
            }

            // Checking to the right of the point
            for (int x = p.X + 1; x <= xMax; x++)
            {
                Point checkable = new Point(x, p.Y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the bottom of the point
            for (int y = p.Y - 1; y >= yMin; y--)
            {
                Point checkable = new Point(p.X, y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the top of the point
            for (int y = p.Y + 1; y <= yMax; y++)
            {
                Point checkable = new Point(p.X, y);
                if (!IsAnyOfAt(checkable, BarierSimplestElements))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        public float GetGlobalWeight(Point point)
        {
            var result = 0;
            int globalDx = Math.Abs(GlobalGoal.X - point.X);
            int globalDy = Math.Abs(GlobalGoal.Y - point.Y);
            float globalDistance =
                (float)Math.Sqrt(globalDx * globalDx + globalDy * globalDy);

            return (globalDistance);
        }


        /// <summary>
        /// Actually, this is the superposition of all weights
        /// </summary>
        public float GetLocalWeight(Point point)
        {
            if (point.IsOutOf(BoardSize))
            {
                return 50000000;
            }
            var result = 0;

            int localDx = Math.Abs(LocalGoal.X - point.X);
            int localDy = Math.Abs(LocalGoal.Y - point.Y);

            float localDistance =
                (float)Math.Sqrt(localDx * localDx + localDy * localDy);

            var resultFloat = result + localDistance + pointsDanger[point] + GetGlobalWeight(point) * 10000;
            return (resultFloat);
        }

        // Find nearest safe point, return the one with 
        // the least threat
        public Point NextMoveNearSimplest()
        {
            var me = GetBomberman();



            var candidates = GetSafePointsNear(me);
            var weights = new float[candidates.Count + 1];

            int index = 1;
            float min = pointsDanger[me];
            weights[0] = min;
            int minIndex = 0;
            Point result = me;
            foreach (var p in GetSafePointsNear(me))
            {
                weights[index] = pointsDanger[p];
                if (index == 0 || weights[index] < min)
                {
                    min = weights[index];
                    minIndex = index;
                    result = p;
                }
                index++;
            }



            return result;
        }

        /// <summary>
        /// Returns all safe cells near the point
        /// </summary>
        public List<Point> GetSafePointsNear(Point point, bool lookForMC = false)
        {
            var result = new List<Point>();
            var me = GetBomberman();
            // check to the left
            Point p = new Point();
            if (point.X > 0)
            {
                p = new Point(point.X - 1, point.Y);
                if (!FutureBlast.Contains(p) && !IsBarrierAt(p) && (!lookForMC || !IsAt(p, Element.MEAT_CHOPPER)))
                {
                    result.Add(p);
                }
            }
            if (point.X < BoardSize)
            {
                p = new Point(point.X + 1, point.Y);
                if (!FutureBlast.Contains(p) && !IsBarrierAt(p) && (!lookForMC || !IsAt(p, Element.MEAT_CHOPPER)))
                {
                    result.Add(p);
                }
            }
            if (point.Y > 0)
            {
                p = new Point(point.X, point.Y - 1);
                if (!FutureBlast.Contains(p) && !IsBarrierAt(p) && (!lookForMC || !IsAt(p, Element.MEAT_CHOPPER)))
                {
                    result.Add(p);
                }
            }
            if (point.Y < BoardSize)
            {
                p = new Point(point.X, point.Y + 1);
                if (!FutureBlast.Contains(p) && !IsBarrierAt(p) && (!lookForMC || !IsAt(p, Element.MEAT_CHOPPER)))
                {
                    result.Add(p);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the list of all safe cells near the point
        /// taking into account ACT before movement
        /// </summary>
        public List<Point> GetSafePointsNearCountMe(Point point)
        {
            var result = new List<Point>();
            var me = GetBomberman();
            // check to the left
            Point p = new Point();
            if (point.X > 0)
            {
                p = new Point(point.X - 1, point.Y);
                if (!FutureBlastAct.Contains(p) && !IsBarrierAt(p) && !IsAt(p, Element.MEAT_CHOPPER))
                {
                    result.Add(p);
                }
            }
            if (point.X < BoardSize)
            {
                p = new Point(point.X + 1, point.Y);
                if (!FutureBlastAct.Contains(p) && !IsBarrierAt(p) && !IsAt(p, Element.MEAT_CHOPPER))
                {
                    result.Add(p);
                }
            }
            if (point.Y > 0)
            {
                p = new Point(point.X, point.Y - 1);
                if (!FutureBlastAct.Contains(p) && !IsBarrierAt(p) && !IsAt(p, Element.MEAT_CHOPPER))
                {
                    result.Add(p);
                }
            }
            if (point.Y < BoardSize)
            {
                p = new Point(point.X, point.Y + 1);
                if (!FutureBlastAct.Contains(p) && !IsBarrierAt(p) && !IsAt(p, Element.MEAT_CHOPPER))
                {
                    result.Add(p);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the point related to the average profit
        /// </summary>
        public Point GetGlobalGoal()
        {
            // Count as 500
            var competitors = GetOtherBombermans();

            // Count as 100
            var MCs = GetMeatChoppers();
            // Count as 10
            var walls = GetDestroyableWalls();

            // Global weights for all cells

            int SumXCompetitor = 0;
            int SumXMC = 0;
            int SumXWall = 0;
            int SumYCompetitor = 0;
            int SumYMC = 0;
            int SumYWall = 0;

            // calculatiion of the average coordinates
            if (competitors.Count > 0)
            {
                foreach (var competitor in competitors)
                {
                    SumXCompetitor += competitor.X;
                    SumYCompetitor += competitor.Y;
                }

                SumXCompetitor /= competitors.Count;
                SumYCompetitor /= competitors.Count;
            }

            if (MCs.Count > 0)
            {
                foreach (var mc in MCs)
                {
                    SumXMC += mc.X;
                    SumYMC += mc.Y;
                }
                SumXMC /= MCs.Count;
                SumYMC /= MCs.Count;
            }
            if (walls.Count > 0)
            {
                foreach (var wall in walls)
                {
                    SumXWall += wall.X;
                    SumYWall += wall.Y;
                }
                SumXWall /= walls.Count;
                SumYWall /= walls.Count;
            }
            int summaryPriceWall = walls.Count * PriceWall;
            int summaryPriceMC = MCs.Count * PriceMC;
            int summaryPriceCompetitor = competitors.Count * PricePlayer;

            // dividing proportionately all average x and y

            float alphaX = summaryPriceCompetitor > summaryPriceWall ?
                summaryPriceCompetitor / (float)summaryPriceWall
                : summaryPriceWall / (float)summaryPriceCompetitor;

            int x = SumXWall < SumXCompetitor ?
                SumXWall + (int)((SumXCompetitor - SumXWall) / (1 + alphaX))
                : SumXCompetitor - (int)((SumXCompetitor - SumXWall) / (1 + alphaX));

            summaryPriceCompetitor += summaryPriceWall;

            alphaX = summaryPriceCompetitor > summaryPriceMC ?
    summaryPriceCompetitor / (float)summaryPriceMC
    : summaryPriceMC / (float)summaryPriceCompetitor;

            x = SumXMC < x ?
                SumXMC + (int)((x - SumXMC) / (1 + alphaX))
                : x - (int)((x - SumXMC) / (1 + alphaX));

            int y = SumYWall < SumYCompetitor ?
    SumYWall + (int)((SumYCompetitor - SumYWall) / (1 + alphaX))
    : SumYCompetitor - (int)((SumYCompetitor - SumYWall) / (1 + alphaX));

            summaryPriceCompetitor += summaryPriceWall;

            float alphaY = summaryPriceCompetitor > summaryPriceMC ?
    summaryPriceCompetitor / (float)summaryPriceMC
    : summaryPriceMC / (float)summaryPriceCompetitor;

            y = SumYMC < y ?
                SumYMC + (int)((y - SumYMC) / (1 + alphaX))
                : y - (int)((y - SumYMC) / (1 + alphaX));

            return new Point(x, y);
        }

        /// <summary>
        /// Returns true if ACT is possible
        /// </summary>
        public bool IsAbleToPlant()
        {
            var me = GetBomberman();
            if (!FutureBlast.Contains(me))
            {

                var result = IsActProfitable();
                var result2 = IsAct2Profitable();
                var index = indexAct();
                if (index == 1)
                {
                    if (result && !MineBombs.ContainsKey(me))
                    {
                        MineBombs.Add(me, 4);
                    }
                    return result;
                }
                else if (index == 2)
                {
                    var aroundMe = GetSafePointsNear(me);

                    return result2;
                }

            }
            return false;
        }

        // Is not important since the Act 
        // after the movement is not implemented
        public int indexAct()
        {
            if (ProfitAct1 == ProfitAct2
                && ProfitAct1 == -50000)
            {
                return 0;
            }
            if (ProfitAct1 > ProfitAct2)
            {
                return 1;
            }
            return 2;
        }

        /// <summary>
        /// Returns true if ACT is profitable 
        /// </summary>
        public bool IsActProfitable()
        {
            var me = GetBomberman();

            var around = GetNearestHits(me);

            foreach (var p in around)
            {
                if (IsAt(p, Element.OTHER_BOMB_BOMBERMAN))
                {
                    return true;
                }
            }


            var enemies = Get(Element.OTHER_BOMBERMAN)
                .Concat(Get(Element.MEAT_CHOPPER))
                .Concat(Get(Element.DESTROYABLE_WALL))
                .ToList();
            int profit = 0;

            if (GetSafePointsNearCountMe(me).Count == 0)
            {
                profit -= 600;
            }

            var mineBlast = GetBlastNearPointSimplest(me);

            foreach (var enemy in enemies)
            {
                if (IsOnBlastLine(me, enemy))
                {


                    if (/*mineBlast.Contains(enemy) && */!GoingToBlow.Contains(enemy))
                    {
                        if (IsAt(enemy, Element.OTHER_BOMBERMAN))
                        {
                            profit += 700;
                        }
                        else if (IsAt(enemy, Element.MEAT_CHOPPER))
                        {
                            profit += 50;
                        }
                        else if (IsAt(enemy, Element.DESTROYABLE_WALL))
                        {
                            profit += 10;
                        }
                    }
                }
            }

            ProfitAct1 = profit;
            return profit > 0;
        }

        /// <summary>
        /// Returns the next point in case of moving before ACT
        /// </summary>
        public Point NextMoveNearV01()
        {
            var me = GetBomberman();
            if (GetSafePointsNear(me).Count == 0)
            {
                return me;
            }
            var result = new Point();
            FindPath01();
            if (Path.Count > 0)
            {
                result = Path.Pop();
            }
            if (FutureBlast.Contains(result))
            {
                var safeNear = GetSafePointsNearCountMe(me);
                if (safeNear.Count == 0)
                {
                    safeNear = GetSafePointsNear(me);
                }
                return safeNear.FirstOrDefault();
            }

            return result;
        }

        /// <summary>
        /// Returns the next point in case of moving after ACT.
        /// This approach is better at finding escapes 
        /// since it doesn't start the search from my location.
        /// </summary>
        public Point NextMoveNearV012()
        {
            var me = GetBomberman();
            if (GetSafePointsNear(me).Count == 0)
            {
                return me;
            }
            var result = new Point();
            FindPath2();
            if (Path.Count > 0)
            {
                result = Path.Pop();
            }
            if (FutureBlast.Contains(result))
            {
                var safeNear = GetSafePointsNearCountMe(me);
                if (safeNear.Count == 0)
                {
                    safeNear = GetSafePointsNear(me);
                }
                return safeNear.FirstOrDefault();
            }

            return result;
        }

        public Point Evade()
        {
            return NextMoveNearV012();
        }

        public List<Point> GetNearestHits(Point p)
        {
            var result = new List<Point>();

            // Calculating the boundaries
            int xMin = p.X < 3 ? 0 : p.X - 3;
            int xMax = p.X > BoardSize - 3 ? BoardSize : p.X + 3;
            int yMin = p.Y < 3 ? 0 : p.Y - 3;
            int yMax = p.Y > BoardSize - 3 ? BoardSize : p.Y + 3;

            // Checking to the left of the point
            for (int x = p.X - 1; x >= xMin; x--)
            {
                Point checkable = new Point(x, p.Y);

                if (IsAnyOfAt(checkable, Element.OTHER_BOMBERMAN, Element.OTHER_BOMB_BOMBERMAN,
                    Element.MEAT_CHOPPER, Element.DESTROYABLE_WALL))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the right of the point
            for (int x = p.X + 1; x <= xMax; x++)
            {
                Point checkable = new Point(x, p.Y);
                if (IsAnyOfAt(checkable, Element.OTHER_BOMBERMAN, Element.OTHER_BOMB_BOMBERMAN,
                    Element.MEAT_CHOPPER, Element.DESTROYABLE_WALL))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the bottom of the point
            for (int y = p.Y - 1; y >= yMin; y--)
            {
                Point checkable = new Point(p.X, y);
                if (IsAnyOfAt(checkable, Element.OTHER_BOMBERMAN, Element.OTHER_BOMB_BOMBERMAN,
                    Element.MEAT_CHOPPER, Element.DESTROYABLE_WALL))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }

            // Checking to the top of the point
            for (int y = p.Y + 1; y <= yMax; y++)
            {
                Point checkable = new Point(p.X, y);
                if (IsAnyOfAt(checkable, Element.OTHER_BOMBERMAN, Element.OTHER_BOMB_BOMBERMAN,
                    Element.MEAT_CHOPPER, Element.DESTROYABLE_WALL))
                {
                    result.Add(checkable);
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        public void FindPath2()
        {
            // first node of the path
            var me = GetBomberman();

            var near = GetSafePointsNear(me);

            Path = new Stack<Point>();
            Path.Push(me);
            var nextPoint = GetOptimalCandidate2(near, 0);

            while (Path.Count > 2)
            {
                Path.Pop();
            }
        }
        public List<Point> UsedPoints2 = new List<Point>();

        public Point GetOptimalCandidate2(IEnumerable<Point> points, int numberOfIteration)
        {
            var goal = LocalGoal;
            if (points.Any(p => (p.X < 0 || p.Y < 0 || p.X > BoardSize || p.Y > BoardSize)))
            {
                return new Point();
            }


            if (numberOfIteration > 100)
            {
                Path.Push(points.FirstOrDefault());
                return Path.Peek();
            }
            foreach (var p in points)
            {
                if (!UsedPoints2.Contains(p))
                {
                    Path.Push(p);
                    UsedPoints2.Add(p);

                    if (p.ShiftLeft() == goal)
                    {
                        Path.Push(p.ShiftLeft());
                        return p.ShiftLeft();
                    }
                    if (p.ShiftTop() == goal)
                    {
                        Path.Push(p.ShiftTop());
                        return p.ShiftTop();
                    }
                    if (p.ShiftRight() == goal)
                    {
                        Path.Push(p.ShiftRight());
                        return p.ShiftRight();
                    }
                    if (p.ShiftBottom() == goal)
                    {
                        Path.Push(p.ShiftBottom());
                        return p.ShiftBottom();
                    }

                    var result = p;
                    var left = p.ShiftLeft();

                    var top = p.ShiftTop();

                    var right = p.ShiftRight();

                    var bottom = p.ShiftBottom();


                    var scores = new float[4] {GetLocalWeight(left),
                    GetLocalWeight(top), GetLocalWeight(right), GetLocalWeight(bottom) };
                    var scorePoints = new Point[4] { left, top, right, bottom };
                    var buf = scores[0];
                    var bufP = scorePoints[0];
                    for (int i = 0; i < scores.Length - 1; i++)
                    {
                        for (int j = i + 1; j < scores.Length; j++)
                        {
                            if (scores[i] > scores[j])
                            {
                                buf = scores[i];
                                scores[i] = scores[j];
                                scores[j] = buf;
                                bufP = scorePoints[i];
                                scorePoints[i] = scorePoints[j];
                                scorePoints[j] = bufP;
                            }
                        }
                    }

                    result = GetOptimalCandidate(scorePoints, numberOfIteration + 1);
                    if (result == LocalGoal)
                    {
                        return result;
                    }
                    else
                    {
                        Path.Pop();
                    }
                }
            }
            return new Point();

        }

        public void FindPath01()
        {
            bool isNeededCalculation = false;
            if (PathAllNodes.Count > 1)
            {
                foreach (var p in PathAllNodes)
                {
                    if (Math.Abs(p.Value - pointsDanger[p.Key]) > 1)
                    {
                        isNeededCalculation = true;
                        break;
                    }
                }
            }
            else
            {
                isNeededCalculation = true;
            }
            if (isNeededCalculation)
            {


                // first node of the path
                var me = GetBomberman();

                var near = new List<Point> { me }
                .Concat(GetSafePointsNear(me));

                var goal = (GetDistance(me, LocalGoal) < GetDistance(me, GlobalGoal)) ?
                   LocalGoal : GlobalGoal;
                if (FutureBlast.Contains(goal) || IsAt(goal, Element.WALL))
                {
                    var nearGoal = GetSafePointsNear(goal);
                    goal = nearGoal.FirstOrDefault();
                }
                currentGoal = goal;

                Path = new Stack<Point>();

                PathAllNodes = new Dictionary<Point, float>();

                var nextPoint = GetOptimalCandidate(near, 0);

                Path.Peek();

                int i = 1;
                foreach (var p in Path.Reverse())
                {
                    if (PathAllNodes.ContainsKey(p))
                    {
                        continue;
                    }
                    if (i < Path.Count)
                    {
                        PathAllNodes.Add(p, pointsDanger[p]);
                    }
                    i++;
                }


                while (Path.Count > 2)
                {
                    Path.Pop();
                }
            }
            else
            {
                Path = new Stack<Point>();

                var stack = new Stack<Point>(PathAllNodes.Keys);
                if (PathAllNodes.Keys.Count < 3)
                {
                    Path = stack;
                }
                else
                {
                    var queue = new Queue<Point>(PathAllNodes.Keys);
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == 0)
                        {
                            queue.Dequeue();
                        }
                        else
                        {
                            Path.Push(queue.Dequeue());
                        }

                    }
                }


            }
        }

        public void FindPath()
        {
            // first node of the path
            var me = GetBomberman();

            var near = new List<Point> { me }
            .Concat(GetSafePointsNear(me));

            var goal = (GetDistance(me, LocalGoal) < GetDistance(me, GlobalGoal)) ?
               LocalGoal : GlobalGoal;
            if (FutureBlast.Contains(goal) || IsAt(goal, Element.WALL))
            {
                var nearGoal = GetSafePointsNear(goal);
                goal = nearGoal.FirstOrDefault();
            }
            currentGoal = goal;

            Path = new Stack<Point>();
            //Path.Push(me);
            var nextPoint = GetOptimalCandidate(near, 0);

            Path.Peek();

            while (Path.Count > 2)
            {
                Path.Pop();
            }
        }
        public List<Point> UsedPoints = new List<Point>();

        public double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }
        public Point GetOptimalCandidate(IEnumerable<Point> points, int numberOfIteration)
        {
            var me = GetBomberman();
            var goal = LocalGoal;


            if (points.Any(p => (p.X < 0 || p.Y < 0 || p.X >= BoardSize || p.Y >= BoardSize)))
            {
                return new Point();
            }


            if (numberOfIteration > 100)
            {
                Path.Push(points.FirstOrDefault());
                return Path.Peek();
            }
            foreach (var p in points)
            {

                if (!UsedPoints.Contains(p))
                {

                    UsedPoints.Add(p);
                    if (IsAt(p, Element.WALL))
                    {
                        continue;
                    }
                    Path.Push(p);
                    if (p.ShiftLeft() == goal)
                    {
                        Path.Push(p.ShiftLeft());
                        return p.ShiftLeft();
                    }
                    if (p.ShiftTop() == goal)
                    {
                        Path.Push(p.ShiftTop());
                        return p.ShiftTop();
                    }
                    if (p.ShiftRight() == goal)
                    {
                        Path.Push(p.ShiftRight());
                        return p.ShiftRight();
                    }
                    if (p.ShiftBottom() == goal)
                    {
                        Path.Push(p.ShiftBottom());
                        return p.ShiftBottom();
                    }

                    var result = p;
                    var left = p.ShiftLeft();

                    var top = p.ShiftTop();

                    var right = p.ShiftRight();

                    var bottom = p.ShiftBottom();

                    var mult = 10000;

                    var scores = new float[4] {pointsDanger[left] + numberOfIteration + mult* (float)GetDistance(left,goal),
                    pointsDanger[top]+ numberOfIteration +  mult*(float)GetDistance(top,goal),
                        pointsDanger[right]+ numberOfIteration +  mult*(float)GetDistance(right,goal),
                        pointsDanger[bottom]+ numberOfIteration +  mult*(float)GetDistance(bottom,goal) };

                    var scorePoints = new Point[4] { left, top, right, bottom };
                    var buf = scores[0];
                    var bufP = scorePoints[0];
                    for (int i = 0; i < scores.Length - 1; i++)
                    {
                        for (int j = i + 1; j < scores.Length; j++)
                        {
                            if (scores[i] > scores[j])
                            {
                                buf = scores[i];
                                scores[i] = scores[j];
                                scores[j] = buf;
                                bufP = scorePoints[i];
                                scorePoints[i] = scorePoints[j];
                                scorePoints[j] = bufP;
                            }
                        }
                    }

                    result = GetOptimalCandidate(scorePoints, numberOfIteration + 1);
                    if (result == LocalGoal)
                    {
                        return result;
                    }
                    else
                    {
                        if (Path.Count > 0)
                        {
                            Path.Pop();
                        }

                    }

                }
            }

            return new Point();

        }

        public bool IsAct2Profitable() => false;
        public bool IsActProfitableV001()
        {
            var me = GetBomberman();

            var around = GetNearestHits(me);

            foreach (var p in around)
            {
                if (IsAt(p, Element.OTHER_BOMB_BOMBERMAN))
                {
                    return true;
                }
            }


            var enemies = Get(Element.OTHER_BOMBERMAN)
                .Concat(Get(Element.MEAT_CHOPPER))
                .Concat(Get(Element.DESTROYABLE_WALL))
                .ToList();
            int profit = 0;

            bool isSafe = GetSafePointsNearCountMe(me).Count > 0;
            if (!isSafe)
            {
                profit -= DeathPenalty;
            }
            foreach (var b in MineBombs.Keys)
            {
                if (IsOnBlastLine(b, me))
                {
                    profit -= DeathPenalty;
                }
            }


            foreach (var enemy in enemies)
            {
                if (IsOnBlastLine(me, enemy))
                {



                    if (IsAt(enemy, Element.OTHER_BOMBERMAN))
                    {
                        profit += PricePlayer;
                    }
                    else if (IsAt(enemy, Element.OTHER_DEAD_BOMBERMAN))
                    {
                        if (!isSafe)
                        {
                            profit -= PriceBombPlayer;
                        }
                        profit += PriceBombPlayer;
                    }
                    else if (IsAt(enemy, Element.MEAT_CHOPPER))
                    {
                        profit += PriceMC;
                    }
                    else if (IsAt(enemy, Element.DESTROYABLE_WALL))
                    {
                        profit += PriceWall;
                    }

                }
            }

            ProfitAct2 = profit;
            return profit > 0;
        }

        public bool IsNoBariersBetween(Point p1, Point p2)
        {
            var xMin = p1.X < p2.X ?
                p1.X : p2.X;
            var yMin = p1.Y < p2.Y ?
               p1.Y : p2.Y;
            var yMax = p1.Y < p2.Y ?
                p2.Y : p1.Y;
            var xMax = p1.X < p2.X ?
                p2.X : p1.X;

            if (xMax - xMin > yMax - yMin)
            {
                for (int x = xMin + 1; x < xMax; x++)
                {
                    if (IsAt(new Point(x, yMin), Element.WALL))
                    {
                        return false;
                    }
                    if (IsAt(new Point(x, yMax), Element.WALL))
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int y = yMin + 1; y < yMax; y++)
                {
                    if (IsAt(new Point(xMin, y), Element.WALL))
                    {
                        return false;
                    }
                    if (IsAt(new Point(xMax, y), Element.WALL))
                    {
                        return false;
                    }
                }
            }


            return true;
        }

        public Point GetLocalGoalSimplest()
        {
            var result = new Point();
            // Search for the closest Goal
            var searchRadius = 20;
            int x = 0, y = 0;
            var me = GetBomberman();
            x = me.X; y = me.Y;
            bool isSearching = true;
            int xMin = 0;
            int xMax = BoardSize;
            int yMin = 0;
            int yMax = BoardSize;
            bool IsMCFound = false;
            bool isPlayerFound = false;
            bool IsDestroyableWallFound = false;
            bool IsMCFoundBarrier = false;
            bool IsDestroyableWallFoundBarrier = false;
            Point MC = new Point();
            Point wall = new Point();
            Point player = new Point();

            Point MCBarrier = new Point();
            Point wallBarrier = new Point();
            int rmin = 1;
            while (isSearching)
            {
                for (int r = 1; r < searchRadius; r++)
                {
                    // check top and bottom line
                    for (y = me.Y - r; y <= me.Y + r; y += 2 * r)
                    {
                        for (x = me.X - r; x <= me.X + r; x++)
                        {
                            var point = new Point(x, y);
                            if (!GoingToBlow.Contains(point))
                            {


                                if (IsAt(point, Element.OTHER_BOMBERMAN))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        return new Point(x, y);
                                    }
                                    else
                                    {
                                        isPlayerFound = true;
                                        player = point;
                                    }
                                }
                                else if (IsAt(point, Element.MEAT_CHOPPER))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        IsMCFound = true;
                                        MC = point;
                                    }
                                    else
                                    {
                                        IsMCFoundBarrier = true;
                                        MCBarrier = point;
                                    }

                                }
                                else if (IsAt(point, Element.DESTROYABLE_WALL))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        IsDestroyableWallFound = true;
                                        wall = point;
                                    }
                                    else
                                    {
                                        IsDestroyableWallFoundBarrier = true;
                                        wallBarrier = point;
                                    }

                                }
                            }
                        }
                    }
                    // check other cells
                    for (y = me.Y - (r - 1); y <= me.Y + (r - 1); y++)
                    {
                        for (x = me.X - r; x <= me.X + r; x += 2 * r)
                        {

                            var point = new Point(x, y);
                            if (!GoingToBlow.Contains(point))
                            {


                                if (IsAt(point, Element.OTHER_BOMBERMAN))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        return new Point(x, y);
                                    }
                                    else
                                    {
                                        isPlayerFound = true;
                                        player = point;
                                    }
                                }
                                else if (IsAt(point, Element.MEAT_CHOPPER))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        IsMCFound = true;
                                        MC = point;
                                    }
                                    else
                                    {
                                        IsMCFoundBarrier = true;
                                        MCBarrier = point;
                                    }

                                }
                                else if (IsAt(point, Element.DESTROYABLE_WALL))
                                {
                                    if (IsNoBariersBetween(me, point))
                                    {
                                        IsDestroyableWallFound = true;
                                        wall = point;
                                    }
                                    else
                                    {
                                        IsDestroyableWallFoundBarrier = true;
                                        wallBarrier = point;
                                    }

                                }
                            }
                        }
                    }
                    if (isPlayerFound)
                    {
                        return player;
                    }
                    if (IsMCFound)
                    {
                        return MC;
                    }
                    else if (IsDestroyableWallFound)
                    {
                        return wall;
                    }
                    else if (IsMCFoundBarrier)
                    {
                        return MCBarrier;
                    }
                    else if (IsDestroyableWallFoundBarrier)
                    {
                        return wallBarrier;
                    }
                }
                rmin = searchRadius;
                searchRadius++;
            }
            return new Point(x, y);
        }
        public void UpdateMineBombs()
        {
            var me = GetBomberman();
            if (!MineBombs.ContainsKey(me))
            {
                MineBombs.Add(me, 5);
            }
        }
        public void AfterAct(bool IsAct2)
        {
            GoingToBlow.Clear();
            foreach (var b in MineBombs.Keys)
            {
                var near = GetNearestHits(b);
                foreach (var point in near)
                {
                    if (!GoingToBlow.Contains(point))
                    {
                        GoingToBlow.Add(point);
                    }
                }
            }

        }

        /// <summary>
        /// Calculate the averaged score
        /// ignoring huge spikes (walls, blasts, 
        /// going to blow objects)
        /// </summary>
        public void ScoreToAverage()
        {
            var keys = new List<Point>(pointsDanger.Keys);
            var newPointsDanger = new Dictionary<Point, float>();


            foreach (var p in keys)
            {
                var near = new List<Point>();

                if (p.X > 0)
                {
                    var left = p.ShiftLeft();
                    if (!IsAt(left, Element.WALL)
                        && !FutureBlast.Contains(left)
                        && !GoingToBlow.Contains(left))
                    {
                        near.Add(left);
                    }

                }
                if (p.Y > 0)
                {
                    var left = p.ShiftBottom();
                    if (!IsAt(left, Element.WALL)
                        && !FutureBlast.Contains(left)
                        && !GoingToBlow.Contains(left))
                    {
                        near.Add(left);
                    }

                }
                if (p.X < BoardSize)
                {
                    var left = p.ShiftRight();
                    if (!IsAt(left, Element.WALL)
                        && !FutureBlast.Contains(left)
                        && !GoingToBlow.Contains(left))
                    {
                        near.Add(left);
                    }
                }
                if (p.Y < BoardSize)
                {
                    var left = p.ShiftTop();
                    if (!IsAt(left, Element.WALL)
                        && !FutureBlast.Contains(left)
                        && !GoingToBlow.Contains(left))
                    {
                        near.Add(left);
                    }
                }
                float averageScore = 0;
                foreach (var n in near)
                {
                    averageScore += GetLocalWeight(n);
                }
                averageScore /= near.Count();
                newPointsDanger.Add(p, averageScore);
            }
            pointsDanger = new Dictionary<Point, float>(newPointsDanger);
        }
    }
}

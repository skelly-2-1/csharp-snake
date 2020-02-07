using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

namespace snake
{
    public partial class snake : Form
    {

        private List<(int x, int y, Stopwatch sw, int expire_time)> foods = new List<(int, int, Stopwatch, int)>();
        private Random rnd = new Random();
        private int move_interval = 100;
        private List<(int x, int y)> snake_position_history = new List<(int, int)>();
        private const int box_size = 10;
        private const int window_size = 500;
        private SolidBrush snake_brush = new SolidBrush(Color.Blue);
        private SolidBrush background_brush = new SolidBrush(Color.Black);
        private SolidBrush dead_brush = new SolidBrush(Color.Red);
        private SolidBrush food_brush = new SolidBrush(Color.Red);
        private const int box_amount = window_size / box_size;
        private static bool snake_move = false;
        private static System.Windows.Forms.Timer move_timer;
        private static System.Windows.Forms.Timer dead_timer;
        private Stopwatch stopwatch = new Stopwatch();
        private const int dead_delay = 3000; // time in ms
        private (int x, int y) snake_head = (0, 0); // snakes current head position
        private int snake_size = 1; // current size of the snake
        private bool snake_died; // did the snake die?
        private bool dead_timer_reset; // does the death timer need to reset?
        private bool snake_just_eaten = false;
        private bool snake_just_eaten_timer_started = false;
        private Stopwatch snake_eaten_timer = new Stopwatch();
        private const int snake_eaten_signal_time = 500; // for x milliseconds, the snake would turn into another color after eating
        private SolidBrush snake_eaten_good_brush = new SolidBrush(Color.Green);
        private SolidBrush snake_eaten_bad_brush = new SolidBrush(Color.Red);
        private SolidBrush snake_eaten_brush;
        private int food_delay = 5000; // base delay -/+ variance / 2
        private int food_variance = 2000; // a variance of 1000 and a delay of 5000 would turn into 4500-5500 for example
        private const int food_decay_time_min = 5000; // minimum decay time of food
        private const int food_decay_time_max = 10000; // maximum decay time of food
        private const int food_decay_variance = 1000; // applies after distance scaling, 1000 would turn into -500 - +500 for example
        private int current_food_delay = -1;
        private int food_generation_chance = 80; // in percent (0-100) - how high is the chance of food generating?

        // for scaling the food time/distance
        private int original_box_amount = 50;
        private int original_speed = 100;

        enum position_state
        {
            POSITION_STATE_NOTHING,
            POSITION_STATE_SNAKE,
            POSITION_STATE_FOOD
        };

        position_state[,] snake_positions = new position_state[box_amount, box_amount];

        bool snake_grown = false;

        public void increase_snake_size()
        {
            if (snake_grown) return;

            snake_grown = true;
            snake_size++;
        }

        enum SNAKE_DIRECTIONS
        {
            SNAKE_DIRECTION_UP,
            SNAKE_DIRECTION_DOWN,
            SNAKE_DIRECTION_LEFT,
            SNAKE_DIRECTION_RIGHT
        };

        SNAKE_DIRECTIONS snake_direction = SNAKE_DIRECTIONS.SNAKE_DIRECTION_RIGHT;

        private void timer_event(Object source, EventArgs e)
        {
            snake_move = true;

            Refresh();
        }

        public void reset_snake()
        {
            snake_position_history.Clear();
            foods.Clear();
            snake_size = 1;

            dead_timer_reset = false;

            if (move_timer != null)
            {
                move_timer.Dispose();
                move_timer = null;
            }

            for (int x = 0; x < box_amount; x++)
            {
                for (int y = 0; y < box_amount; y++)
                {
                    var middle = (x == box_amount / 2 && y == box_amount / 2);

                    snake_positions[x, y] = middle ? position_state.POSITION_STATE_SNAKE : position_state.POSITION_STATE_NOTHING;

                    if (middle)
                    {
                        snake_head.x = x;
                        snake_head.y = y;
                    }
                }
            }

            move_timer = new System.Windows.Forms.Timer();
            move_timer.Interval = (int)((double)move_interval * ((double)box_size / 10.0));
            move_timer.Tick += new System.EventHandler(timer_event);
            move_timer.Start();

            current_food_delay = rnd.Next(food_delay - food_variance / 2, food_delay + food_variance / 2);

            stopwatch.Stop();
            stopwatch.Start();
        }

        public void check_and_create_food()
        {
            //Debug.WriteLine($"food amount: {foods.Count}");

            // remove expired food
            for (int i = foods.Count - 1; i >= 0; i--)
            {
                var f = foods[i];

                //Debug.WriteLine($"expired: {(int)f.sw.Elapsed.TotalMilliseconds}, expire time: {f.expire_time}");

                if (f.sw.Elapsed.TotalMilliseconds < (double)f.expire_time) continue;

                //MessageBox.Show("removed food");

                Debug.WriteLine("removed food");

                foods.RemoveAt(i);

                snake_positions[f.x, f.y] = position_state.POSITION_STATE_NOTHING;
            }

            // check if it's time to generate new food
            if (stopwatch.Elapsed.TotalMilliseconds < current_food_delay) return;

            // generate food
            if (rnd.Next(0, 100) >= (100 - food_generation_chance))
            {
                while (true)
                {
                    var x = rnd.Next(0, box_amount - 1);
                    var y = rnd.Next(0, box_amount - 1);

                    if (snake_positions[x, y] == position_state.POSITION_STATE_NOTHING)
                    {
                        // calculate distance scaling for the maximum amount of time the food can be there
                        var max_distance = box_amount + box_amount;
                        var distance_x = x - snake_head.x;

                        if (distance_x < 0) distance_x *= -1;

                        var distance_y = y - snake_head.y;

                        if (distance_y < 0) distance_y *= -1;

                        var distance = distance_x + distance_y;
                        var distance_scale = (double)distance / (double)max_distance;

                        // now we can calculate the decay time
                        var decay_time = (int)((double)food_decay_time_min + (((double)food_decay_time_max - (double)food_decay_time_min) * distance_scale));

                        // randomize decay time with variance
                        decay_time = rnd.Next(decay_time - food_decay_variance / 2, decay_time + food_decay_variance / 2);

                        var sw = new Stopwatch();

                        sw.Start();

                        foods.Add((x, y, sw, decay_time));

                        snake_positions[x, y] = position_state.POSITION_STATE_FOOD;

                        break;
                    }
                }
            }

            // set new random food delay
            current_food_delay = rnd.Next(food_delay - food_variance / 2, food_delay + food_variance / 2);

            stopwatch.Restart();
        }

        public snake()
        {
            InitializeComponent();

            snake_died = false;

            reset_snake();

            // enable double buffering to prevent flickering
            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, this, new object[] { true });
        }

        private void dead_event(Object source, EventArgs e)
        {
            snake_died = false;
            dead_timer_reset = true;
        }

        void kill_snake()
        {
            snake_died = true;

            reset_snake();

            dead_timer = new System.Windows.Forms.Timer();
            dead_timer.Interval = dead_delay;
            dead_timer.Tick += new System.EventHandler(dead_event);
            dead_timer.Start();
        }

        private bool remove_food_at_coordinates(int x, int y)
        {
            // remove food from our list
            for (int i = 0; i < foods.Count; i++)
            {
                var f = foods[i];

                if (f.x == x && f.y == y)
                {
                    foods.RemoveAt(i);

                    return true;
                }
            }

            return false;
        }

        private void eat(int x, int y)
        {
            increase_snake_size();

            remove_food_at_coordinates(snake_head.x, snake_head.y);

            snake_just_eaten_timer_started = false;
            snake_just_eaten = true;
        }

        void move_snake()
        {
            if (snake_position_history.Count == snake_size) snake_position_history.RemoveAt(0);

            snake_position_history.Add(snake_head);

            var last_pos = snake_position_history[0];

            if (!snake_grown)
            {
                snake_positions[last_pos.x, last_pos.y] = position_state.POSITION_STATE_NOTHING;
            }
            else
            {
                snake_grown = false;
            }

            switch (snake_direction)
            {
                case SNAKE_DIRECTIONS.SNAKE_DIRECTION_DOWN:
                    if (snake_head.y + 1 >= box_amount)
                    {
                        kill_snake();
                    }
                    else
                    {
                        snake_head.y++;

                        if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_SNAKE)
                        {
                            kill_snake();
                        }
                        else
                        {
                            if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_FOOD) eat(snake_head.x, snake_head.y);

                            snake_positions[snake_head.x, snake_head.y] = position_state.POSITION_STATE_SNAKE;
                        }
                    }

                    break;
                case SNAKE_DIRECTIONS.SNAKE_DIRECTION_UP:
                    if (snake_head.y <= 0)
                    {
                        kill_snake();
                    }
                    else
                    {
                        snake_head.y--;

                        if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_SNAKE)
                        {
                            kill_snake();
                        }
                        else
                        {
                            if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_FOOD) eat(snake_head.x, snake_head.y);

                            snake_positions[snake_head.x, snake_head.y] = position_state.POSITION_STATE_SNAKE;
                        }
                    }

                    break;
                case SNAKE_DIRECTIONS.SNAKE_DIRECTION_LEFT:
                    if (snake_head.x <= 0)
                    {
                        kill_snake();
                    }
                    else
                    {
                        snake_head.x--;

                        if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_SNAKE)
                        {
                            kill_snake();
                        }
                        else
                        {
                            if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_FOOD) eat(snake_head.x, snake_head.y);

                            snake_positions[snake_head.x, snake_head.y] = position_state.POSITION_STATE_SNAKE;
                        }
                    }

                    break;
                default: // right
                    if (snake_head.x + 1 >= box_amount/* - 1*/)
                    {
                        kill_snake();
                    }
                    else
                    {
                        snake_head.x++;

                        if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_SNAKE)
                        {
                            kill_snake();
                        }
                        else
                        {
                            if (snake_positions[snake_head.x, snake_head.y] == position_state.POSITION_STATE_FOOD) eat(snake_head.x, snake_head.y);

                            snake_positions[snake_head.x, snake_head.y] = position_state.POSITION_STATE_SNAKE;
                        }
                    }

                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // if the snake isn't dead, check and generate new food
            if (!snake_died) check_and_create_food();

            // if the snake needs to move, move it
            if (snake_move)
            {
                move_snake();

                snake_move = false;
            }

            // if the snake is alive and the death timer needs to reset, reset the timer
            if (!snake_died && dead_timer_reset)
            {
                if (dead_timer != null)
                {
                    dead_timer.Stop();
                    dead_timer.Dispose();
                    dead_timer = null;
                }

                dead_timer_reset = false;
            }

            // did we start the eaten timer?
            if (!snake_just_eaten_timer_started && snake_just_eaten)
            {
                snake_just_eaten_timer_started = true;

                snake_eaten_timer.Stop();
                snake_eaten_timer.Start();
            }

            // draw base panel/rectangle
            e.Graphics.FillRectangle(background_brush, new Rectangle(0, 0, box_size * box_amount, box_size * box_amount));

            // now fill in individual pixels according to our data
            for (int x = 0; x < window_size; x += box_size)
            {
                for (int y = 0; y < window_size; y += box_size)
                {
                    var brush = background_brush;
                    var x_ = x / box_size;
                    var y_ = y / box_size;

                    if (snake_died)
                    {
                        brush = dead_brush;
                    }
                    else if (snake_positions[x_, y_] == position_state.POSITION_STATE_FOOD)
                    {
                        brush = food_brush;
                    }
                    else if (snake_positions[x_, y_] == position_state.POSITION_STATE_SNAKE)
                    {
                        brush = snake_brush;

                        if (snake_just_eaten)
                        {
                            snake_eaten_brush = snake_eaten_good_brush; // change if other types of foods are added
                            brush = snake_eaten_brush;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    e.Graphics.FillRectangle(brush, new Rectangle(x, y, box_size, box_size));
                }
            }

            // reset snake color if the time is over after eating
            if (snake_eaten_timer.Elapsed.TotalMilliseconds >= snake_eaten_signal_time) snake_just_eaten = snake_just_eaten_timer_started = false;
        }

        private void set_direction(SNAKE_DIRECTIONS dir)
        {
            if ((dir == SNAKE_DIRECTIONS.SNAKE_DIRECTION_RIGHT && snake_direction == SNAKE_DIRECTIONS.SNAKE_DIRECTION_LEFT ||
                dir == SNAKE_DIRECTIONS.SNAKE_DIRECTION_LEFT && snake_direction == SNAKE_DIRECTIONS.SNAKE_DIRECTION_RIGHT ||
                dir == SNAKE_DIRECTIONS.SNAKE_DIRECTION_UP && snake_direction == SNAKE_DIRECTIONS.SNAKE_DIRECTION_DOWN ||
                dir == SNAKE_DIRECTIONS.SNAKE_DIRECTION_DOWN && snake_direction == SNAKE_DIRECTIONS.SNAKE_DIRECTION_UP) &&
                snake_size > 1) return;

            snake_direction = dir;
        }

        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_SYSKEYDOWN = 0x104;

        protected override bool ProcessCmdKey(ref Message message, Keys keycode)
        {
            if (message.Msg == WM_KEYDOWN || message.Msg == WM_SYSKEYDOWN)
            {
                /*if (keycode == Keys.Enter)
                {
                    increase_snake_size();

                    return true;
                }
                else */if (keycode == Keys.Up || keycode == Keys.W)
                {
                    set_direction(SNAKE_DIRECTIONS.SNAKE_DIRECTION_UP);
                }
                else if (keycode == Keys.Down || keycode == Keys.S)
                {
                    set_direction(SNAKE_DIRECTIONS.SNAKE_DIRECTION_DOWN);
                }
                else if (keycode == Keys.Left || keycode == Keys.A)
                {
                    set_direction(SNAKE_DIRECTIONS.SNAKE_DIRECTION_LEFT);
                }
                else if (keycode == Keys.Right || keycode == Keys.D)
                {
                    set_direction(SNAKE_DIRECTIONS.SNAKE_DIRECTION_RIGHT);
                }
            }

            return base.ProcessCmdKey(ref message, keycode);
        }

    }
}

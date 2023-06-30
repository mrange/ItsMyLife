# Conway's Game of Life in Avalonia + F#

Hello! Some time ago, I noticed that Khalid (@khalidabuhakmeh@mastodon.social) was experimenting with Avalonia and Conway's Game of Life on Mastodon. Khalid used the C# MVVM template for Avalonia. Later, brandewinder (@brandewinder@hachyderm.io) joined and implemented his version using F# with Elmish. They had an interesting discussion on Mastodon about performance, which inspired me to give it a try as well.

## My Approach

Instead of using MVVM or Elmish, I decided to take the custom control route. I have experience with this approach from WPF, and it's the fastest way I know to render a large number of rectangles. I thought it might work well in Avalonia too.

## Avalonia

I hadn't really tried Avalonia before, but to my pleasant surprise, as someone who worked extensively with WPF around 2010, I felt right at home. Creating a custom control in Avalonia is very similar to how it's done in WPF.

## Let's Get Started

First, I created my control:

```fsharp
type ItIsMyLifeControl () =
  class
    inherit Control ()
    // Code goes here
  end
```

Then, I set this control as the content control of the MainWindow:

```fsharp
type App() =
    inherit Application()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
          let window          = MainWindow ()
          // Creating my control and setting it as the MainWindow's content
          let control         = ItIsMyLifeControl ()
          window.Content      <- control
          desktop.MainWindow  <- window
          control.Start ()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
```

## Rendering the Game of Life

To have something to render, I need the state of the Game of Life. The state is a grid of cells, where each cell is represented by a byte. A value of 0 means the cell is dead, and values from 1 to 255 represent the age of an alive cell.

In my control, I override the `Control.Render` method and use the `DrawingContext` to draw a lot of rectangles on the screen.

```fsharp
type ItIsMyLifeControl () =
  class
    inherit Control ()

    let [<Literal>] _dead     = 0uy
    let [<Literal>] _infant   = 1uy
    let [<Literal>] _width    = 256
    let [<Literal>] _height   = 256
    let [<Literal>] _timeout  = 0.05

    let mutable _current      = Array.zeroCreate<byte> (_width * _height)
    let mutable _next         = Array.zeroCreate<byte> (_width * _height)

    let _brushes : IBrush array =
      // An array of brushes for each age
      // Details left out
      null

    // Custom render method
    override x.Render (context : DrawingContext) =
      let b           = x.Bounds
      let w           = float _width
      let h           = float _height
      // Find the cell screen size
      let cellWidth   = Math.Floor (b.Width / w)
      let cellHeight  = Math.Floor (b.Height / h)
      let cell        = Math.Min (cellWidth, cellHeight)

      let totWidth    = cell * w
      let totHeight   = cell * h



      // Offset to center the state on the screen
      let offX        = Math.Round ((b.Width - totWidth) * 0.5)
      let offY        = Math.Round ((b.Height - totHeight) * 0.5)

      // Loops through each cell, 1960s style
      for y = 0 to _height - 1 do
        let yoff = y * _width
        for x = 0 to _width - 1 do
          let current = _current.[x + yoff]
          // If the cell is dead, we don't render it (improves performance)
          if current <> _dead then
            // Otherwise, pick a brush based on the cell's age
            let brush = _brushes.[int current]
            context.DrawRectangle (brush, null, new Rect (offX + float x * cell, offY + float y * cell, cell - 1., cell - 1.))
      base.Render context

  end
```

## Let's Start Fresh

For the initial state, I simply flip a coin for each cell to determine if it's alive or dead.

```fsharp
// Let's start fresh
let ragnarök () =
  let rnd = Random.Shared
  for y = 0 to _height - 1 do
    let yoff = y * _width
    for x = 0 to _width - 1 do
      let isAlive = rnd.NextDouble () > 0.5
      _current.[x + yoff] <- if isAlive then _infant else _dead
```

## Evolution

To evolve the game state, we need to go through each cell and count how many of its 8 neighbors are alive. Then, we apply Conway's rules:

1. Any live cell with fewer than two live neighbors dies, as if by underpopulation.
2. Any live cell with two or three live neighbors lives on to the next generation.
3. Any live cell with more than three live neighbors dies, as if by overpopulation.
4. Any dead cell with exactly three live neighbors becomes a live cell, as if by reproduction.

We put the new state in the `_next` state array to avoid changing `_current` while computing the new state. At the end, we swap the `_current` and `_next` buffers using the classic double-buffering pattern.

I used 1960s-style loops to iterate through the cells.

```fsharp
// Evolves from one state to the next
let evolve () =
  // Iterate through all cells, 1960s style
  for y = 0 to _height - 1 do
    let yoff = y * _width
    for x = 0 to _width - 1 do
      // Count alive neighbors, 1960s style
      let mutable aliveNeighbours = 0
      for yy = -1 to 1 do
        let fy = (_height + y + yy) % _height
        let fyoff = fy * _width

        for xx = -1 to 1 do
          let fx = (_width + x + xx) % _width
          let inc = if _current.[fx + fyoff] <> _dead then 1 else 0
          aliveNeighbours <- aliveNeighbours + inc

      let current = _current.[yoff + x]
      // If the current cell is alive, the count of alive neighbors is incremented by 1
      // because the loop above covers all cells in a 3x3 block, including the current cell
      let dec = if current <> _dead then 1 else 0
      aliveNeighbours <- aliveNeighbours - dec

      // If the cell survives, increment its age by 1
      let aliveAndWell = Math.Min (current, 254uy) + 1uy

      // Apply Conway's rules
      let next

 =
        match aliveNeighbours with
        | 0 | 1 -> _dead
        | 2     -> if current = _dead then _dead else aliveAndWell
        | 3     -> aliveAndWell
        | _     -> _dead
      _next.[yoff + x] <- next

  // Swap current and next buffers, 1960s style
  let tmp = _current
  _current <- _next
  _next <- tmp
```

## Almost Done

Next, I create a timer to compute a new state and invalidate the visual to redraw the screen. Once again, as a WPF developer, this feels very familiar.

```fsharp
let onTimer (sender : obj) (e : EventArgs) =
  evolve ()
  // We can't directly call base class methods from here
  // We can assign the "this" reference a name, but that has other problems in F#
  // Therefore, we delegate the invalidate visual request
  _invalidateVisual ()

let _timer = new DispatcherTimer (
    TimeSpan.FromSeconds _timeout
  , DispatcherPriority.SystemIdle
  , EventHandler onTimer
  )
```

Finally, the `Start` method:

```fsharp
member x.Start () =
  ragnarök ()
  evolve ()
  // Here we can set up the invalidate visual redirect because
  // we have access to the "this" reference here.
  _invalidateVisual <- fun () -> x.InvalidateVisual ()
  _timer.Start ()
```

## How Did It Go?

I think it was a fun little exercise, and performance-wise, it does decently at 256x256 and 20 FPS (around 1%-5% CPU usage on my machine after a few iterations).

I also think Avalonia is pretty sweet, coming from a former WPF developer.
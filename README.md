# Conway's game of life in Avalonia + F#

Hello. Some time ago I saw that Khalid(@khalidabuhakmeh@mastodon.social) was playing around with Avalonia and Conway's game of life on Mastodon.

Khalid used the C# MVVM template for Avalonia.

Later brandewinder (@brandewinder@hachyderm.io) joined and implemented his F# version using Elmish.

They had a pretty interesting discussion on Mastodon over performance which made me want to make an attempt as well.

## My take on it

So instead of using MVVM or Elmish I decided to try the custom control route. This I have experience with from WPF and is the fastest way I know of to render lot of rectangles. I thought maybe it's true for Avalonia as well.

## Avalonia

I never really tried Avalonia before but a pleasant surprise was that as someone that did alot of WPF around 2010 I felt right at home and creating a custom control works very similar in Avalonia compared to how it works in WPF.

## Let's go

I created my control:

```fsharp
type ItIsMyLifeControl () =
  class
    inherit Control ()
    // Code goes here
  end
```

Then I set this control as the content control of the mainwindow

```fsharp
type App() =
    inherit Application()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
          let window          = MainWindow ()
          // Creating my control and setting it as the mainwindow content
          let control         = ItIsMyLifeControl ()
          window.Content      <- control
          desktop.MainWindow  <- window
          control.Start ()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
```

## Rendering the game of life

To have something to render I need the game of life state. The state is a grid of cells where each cell is a byte where 0 means dead and 1 to 255 is the age of an alive cell.

Then I override `Control.Render` and through the `DrawingContext` draws alot of rectangles on screen.

```fsharp
type ItIsMyLifeControl () =
  class
    inherit Control ()

    let [<Literal>] _dead     = 0uy
    let [<Literal>] _infant   = 1uy
    let [<Literal>] _width    = 256
    let [<Literal>] _height   = 256
    let [<Literal>] _timeout  = (0.05)

    let mutable _current      = Array.zeroCreate<byte> (_width*_height)
    let mutable _next         = Array.zeroCreate<byte> (_width*_height)

    let _brushes : IBrush array =
      // An array of brushes for each age
      //  Details left out
      null

    // Custom render method
    override x.Render (context : DrawingContext) =
      let b           = x.Bounds
      let w           = float _width
      let h           = float _height
      // Find a the cell screen size
      let cellWidth   = Math.Floor (b.Width  / w)
      let cellHeight  = Math.Floor (b.Height / h)
      let cell        = Math.Min   (cellWidth, cellHeight)

      let totWidth    = cell*w
      let totHeight   = cell*h

      // An offset so the state is centered on screen
      let offX        = Math.Round ((b.Width  - totWidth)*0.5)
      let offY        = Math.Round ((b.Height - totHeight)*0.5)

      // Loops through each cell 1960s style
      for y = 0 to _height - 1 do
        let yoff = y*_width
        for x = 0 to _width - 1 do
          let current = _current.[x + yoff]
          // If dead we don't render, helps performance
          if current <> _dead then
            // Otherwise pick a brush using the cell age
            let brush = _brushes.[int current]
            context.DrawRectangle (brush, null, new Rect (offX+float x*cell, offY+float y*cell, cell-1., cell-1.))
      base.Render context

  end
```

## Let's have a fresh start

For the initial state I simply flip a coin for each cell and from the result determines if a cell is alive or dead.

```fsharp
// Let's have a fresh new start
let ragnarök () =
  let rnd = Random.Shared
  for y = 0 to _height - 1 do
    let yoff = y*_width
    for x = 0 to _width - 1 do
      let isAlive = rnd.NextDouble () > 0.5
      _current.[x + yoff] <- if isAlive then _infant else _dead
```

## Evolution

Then to evolve the game state we need to go through each cell and count how many of its 8 neighbours are alive. Then we apply the Conway's rules to determine if the cell survives, dies or is resurrected.

The new state we put in the `_next` state as to not change `_current` while we are computing a new state.

At the end we flip the `_current` and `_next` state in the classic double buffering pattern.

I used 1960s style for loops to iterate through the cells.

```fsharp
// evolves from one state into the next
let evolve () =
  // Iterates through all cells 1960s style
  for y = 0 to _height - 1 do
    let yoff = y*_width
    for x = 0 to _width - 1 do
      // Counts alive neighbours 1960s style
      let mutable aliveNeighbours = 0
      for yy = -1 to 1 do
        let fy    = (_height + y + yy)%_height
        let fyoff = fy*_width

        for xx = -1 to 1 do
          let fx  = (_width + x + xx)%_width
          let inc = if _current.[fx + fyoff] <> _dead then 1 else 0
          aliveNeighbours <- aliveNeighbours + inc

      let current = _current.[yoff + x]
      // If the current cell is alive the alive neighbours is +1
      //  because the loop above loops over all cells in 3x3 block
      //  including current
      let dec = if current <> _dead then 1 else 0
      aliveNeighbours <- aliveNeighbours - dec

      // If the cell survives increment it's age by 1
      let aliveAndWell = Math.Min (current, 254uy) + 1uy

      // The Conway rules
      let next =
        match aliveNeighbours with
        | 0 | 1 -> _dead
        | 2     -> if current = _dead then _dead else aliveAndWell
        | 3     -> aliveAndWell
        | _     -> _dead
      _next.[yoff + x] <- next

  // Swaps current and next buffers, 1960s style
  let tmp   = _current
  _current  <- _next
  _next     <- tmp
```

## Almost done

Then I created a timer to compute a new state and invalidate the visual to redraw the screen. Once again as a WPF dev this feels very familiar.

```fsharp
let onTimer s e =
  evolve ()
  // We can't reach the base class methods from here
  //  We could assign the "this" ref a name but that
  //  has other problems in F#
  //  Delegate the invalidate request
  _invalidateVisual ()

let _timer = new DispatcherTimer (
    TimeSpan.FromSeconds _timeout
  , DispatcherPriority.SystemIdle
  , (EventHandler onTimer)
  )
```

And finally the `Start` method:

```fsharp
member x.Start () =
  ragnarök ()
  evolve ()
  // Here we can set up the invalidate visual redirect because
  // we have access to the "this" ref here.
  _invalidateVisual <- fun () -> x.InvalidateVisual ()
  _timer.Start ()
```

## How did it go?

I think it was a fun little excersise and performance-wise it does decent at 256x256 at 20FPS (around 1%-5% CPU on my machine after a few iterations).

I also think Avalonia is pretty sweet as a former WPF dev.

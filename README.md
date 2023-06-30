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



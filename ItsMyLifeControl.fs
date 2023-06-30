namespace ItsMyLife

open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading

open System

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

    let mutable _invalidateVisual = fun () -> ()

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

    // Let's have a fresh new start
    let ragnarök () =
      let rnd = Random.Shared
      for y = 0 to _height - 1 do
        let yoff = y*_width
        for x = 0 to _width - 1 do
          let isAlive = rnd.NextDouble () > 0.5
          _current.[x + yoff] <- if isAlive then _infant else _dead

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

    // Initialize 256 brushes with a color of each age
    //  the color of age 0 is black (dead)
    //  the color of age 1 is white (infant)
    //  then the color is generated through a blackbody radiation function
    //  didn't look as cool as a I hoped.
    let _brushes : IBrush array = 
      // License: CC BY-NC-SA 3.0, author: Stephane Cuillerdier - Aiekick/2015 (twitter:@aiekick), found: https://www.shadertoy.com/view/Mt3GW2
      let blackbodyRadiation temp =
        let mutable x = 56100000.0 * Math.Pow (temp, (-3.0 / 2.0)) + 148.0
        let mutable y = 100.04 * Math.Log temp - 623.6
        let mutable z = 194.18 * Math.Log temp - 1448.6

        if temp > 6500.0 then
          y <- 35200000.0 * Math.Pow (temp, (-3.0 / 2.0)) + 184.0

        x <- Math.Clamp (x, 0.0, 255.0)
        y <- Math.Clamp (y, 0.0, 255.0)
        z <- Math.Clamp (z, 0.0, 255.0)

        if temp < 1000.0 then
          let tt = temp/1000.0
          x <- x * tt
          y <- y * tt
          z <- z * tt

        Color.FromRgb (byte x, byte y, byte z)

      let init n = 
        let b =
          match n with
          | 0 -> Brushes.Black
          | 1 -> Brushes.White
          | _ -> 
            let max   = 8000.0
            let min   = 200.0
            let ratio = max/min

            let i     = float n
            let ii    = (i-2.0)/253.0

            let temp  = max*Math.Exp (-ii*Math.Log ratio)
            let col   = blackbodyRadiation temp
            SolidColorBrush col
        b :> IBrush
      Array.init 256 init

    member x.Start () =
      ragnarök ()
      evolve ()
      // Here we can set up the invalidate visual redirect because
      // we have access to the "this" ref here.
      _invalidateVisual <- fun () -> x.InvalidateVisual ()
      _timer.Start ()

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


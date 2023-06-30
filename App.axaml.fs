namespace ItsMyLife

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

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

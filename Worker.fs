namespace Worker

open System
open System.Collections.Generic
open System.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type Worker( logger: ILogger<_> , appLifetime : IHostApplicationLifetime) as this =

    [<DefaultValue>] val mutable applicationTask : Task
    [<DefaultValue>] val mutable exitCode : Nullable<int>

    interface IHostedService with

        member _.StartAsync(ct: CancellationToken): Task =

            task {

                let mutable cancellationTokenSource = Unchecked.defaultof<CancellationTokenSource>

                appLifetime.ApplicationStarted.Register(fun _ ->

                logger.LogDebug("Application has started")

                cancellationTokenSource <- CancellationTokenSource.CreateLinkedTokenSource(ct)

                this.applicationTask <-
                    task {
                        try
                            try

                                // 1.normal
                                logger.LogInformation "Hello World!"

                                // 2.error
                                // failwith "my error!"

                                // 3.user cancel
                                do! task {
                                    while true do
                                        cancellationTokenSource.Token.ThrowIfCancellationRequested()
                                        $"{DateTime.Now}" |> logger.LogInformation
                                        do! Task.Delay 1000
                                }

                                this.exitCode <- Nullable(0)
                            with
                                | :? TaskCanceledException as ex ->
                                    // This means the application is shutting down, so just swallow this exception
                                    ()
                                | :? OperationCanceledException as ex ->
                                    logger.LogError(ex, "USER'S CANCEL!");
                                | _ as ex ->
                                    logger.LogError(ex, "MY ERROR!");
                                    this.exitCode <- Nullable(1)
                        finally
                            // Stop the application once the work is done
                            appLifetime.StopApplication()
                    }
                    :> Task

                ) |> ignore

                appLifetime.ApplicationStopping.Register(fun _ ->
                    logger.LogInformation("Application is stopping")
                    cancellationTokenSource.Cancel()
                ) |> ignore

                return Task.CompletedTask

            }
            :> Task


        member _.StopAsync(ct: CancellationToken): Task =
            task {

                logger.LogInformation($"Exiting with return code: {this.exitCode}");

                // Wait for the application logic to fully complete any cleanup tasks.
                // Note that this relies on the cancellation token to be properly used in the application.
                do! this.applicationTask

                // Exit code may be null if the user cancelled via Ctrl+C/SIGTERM
                Environment.ExitCode <- this.exitCode.GetValueOrDefault(-1)

            }
            :> Task

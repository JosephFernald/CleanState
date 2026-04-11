// =============================================================================
// CleanState Sample: UI Flow Orchestration
// =============================================================================
//
// This sample demonstrates how CleanState replaces the typical mess of:
//
//   bool isWaiting;
//   bool isAnimating;
//   bool hasConfirmed;
//   bool permissionsGranted;
//   int currentStep;
//
// ── The problem ──
//
//   Multi-step UI flows (onboarding, modals, wizards) become tangled webs of
//   booleans, coroutines, and callbacks. Nobody can tell what step the user is
//   on, what they chose, or why the flow is stuck.
//
// ── The CleanState approach ──
//
//   A single, readable pipeline. Every step is named.
//   Every wait is explicit. Branching is declarative.
//   The debugger shows exactly where the flow is at any moment.
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Identity;
using CleanState.Steps;

namespace CleanState.Samples.UIFlow;

class Program
{
    static readonly Random Rng = new(123);

    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║       CleanState Sample: UI Flow Orchestration      ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  A multi-step onboarding flow with:                 ║");
        Console.WriteLine("║  - Welcome screen → permissions → user choices      ║");
        Console.WriteLine("║  - Async API call simulation                        ║");
        Console.WriteLine("║  - Conditional branching based on user input        ║");
        Console.WriteLine("║  - No booleans, no coroutines, no spaghetti.        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build the onboarding flow ──

        var definition = new MachineBuilder("OnboardingFlow")

            .State("Welcome")
                .TransitionIn(ctx =>
                {
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│     Welcome to MyApp!        │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  Let's get you set up.       │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│       [ Continue ]           │");
                    Print("SCREEN", "└──────────────────────────────┘");
                }, "ShowWelcomeScreen")
                .WaitForEvent("UserTapped", "WaitForWelcomeTap")
                .Then(ctx =>
                {
                    Print("ACTION", "User tapped Continue.");
                }, "AckWelcome")
                .GoTo("Permissions", "GoToPermissions")

            .State("Permissions")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│  We need a few permissions:  │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  ☐ Notifications             │");
                    Print("SCREEN", "│  ☐ Location                  │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  [ Allow ]    [ Skip ]       │");
                    Print("SCREEN", "└──────────────────────────────┘");
                }, "ShowPermissionsScreen")
                .WaitForEvent("PermissionResponse", "WaitForPermission")
                .Then(ctx =>
                {
                    // Simulate user granting or skipping
                    bool granted = Rng.Next(2) == 1;
                    ctx.Set("permissionsGranted", granted);
                    Print("ACTION", granted
                        ? "User granted permissions."
                        : "User skipped permissions.");
                }, "ProcessPermissionResponse")
                .Decision("PermissionDecision")
                    .When(ctx => ctx.Get<bool>("permissionsGranted"), "UserPreferences", "PermissionsGranted")
                    .Otherwise("PermissionNudge", "PermissionsSkipped")

            .State("PermissionNudge")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│  Are you sure?               │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  Without notifications,      │");
                    Print("SCREEN", "│  you'll miss updates!        │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  [ Enable ]   [ No thanks ]  │");
                    Print("SCREEN", "└──────────────────────────────┘");
                }, "ShowNudgeScreen")
                .WaitForEvent("NudgeResponse", "WaitForNudge")
                .Then(ctx =>
                {
                    Print("ACTION", "User acknowledged nudge.");
                }, "AckNudge")
                .GoTo("UserPreferences", "GoToPreferences")

            .State("UserPreferences")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│  What interests you?         │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│  ○ Technology                │");
                    Print("SCREEN", "│  ○ Sports                    │");
                    Print("SCREEN", "│  ○ Music                     │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│       [ Next ]               │");
                    Print("SCREEN", "└──────────────────────────────┘");
                }, "ShowPreferencesScreen")
                .WaitForEvent("PreferencesSubmitted", "WaitForPreferences")
                .Then(ctx =>
                {
                    // Simulate user selection
                    var choices = new[] { "Technology", "Sports", "Music" };
                    var choice = choices[Rng.Next(choices.Length)];
                    ctx.Set("userInterest", choice);
                    Print("ACTION", $"User selected: {choice}");
                }, "ProcessPreferences")
                .GoTo("ProfileSetup", "GoToProfileSetup")

            .State("ProfileSetup")
                .TransitionIn(ctx =>
                {
                    var interest = ctx.Get<string>("userInterest");
                    Console.WriteLine();
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│  Setting up your profile...  │");
                    Print("SCREEN", $"│  Interest: {interest,-19}│");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│        ◌ Loading...          │");
                    Print("SCREEN", "└──────────────────────────────┘");
                    Print("API", "Calling /api/profile/create...", ConsoleColor.DarkCyan);
                }, "ShowLoadingScreen")
                .WaitForTime(2.0f, "SimulateApiCall")
                .Then(ctx =>
                {
                    Print("API", "Response: 200 OK — profile created.", ConsoleColor.DarkCyan);
                    ctx.Set("profileCreated", true);
                }, "HandleApiResponse")
                .GoTo("Complete", "GoToComplete")

            .State("Complete")
                .TransitionIn(ctx =>
                {
                    var interest = ctx.Get<string>("userInterest");
                    var perms = ctx.TryGet<bool>("permissionsGranted", out var granted) && granted;
                    Console.WriteLine();
                    Print("SCREEN", "┌──────────────────────────────┐");
                    Print("SCREEN", "│  You're all set!             │");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", $"│  Interest: {interest,-19}│");
                    Print("SCREEN", $"│  Notifications: {(perms ? "ON " : "OFF"),-14}│");
                    Print("SCREEN", "│                              │");
                    Print("SCREEN", "│      [ Get Started ]         │");
                    Print("SCREEN", "└──────────────────────────────┘");
                }, "ShowCompleteScreen")
            .Build();

        // ── Run the flow ──

        var traceBuffer = new TraceBuffer(32);
        var scheduler = new Scheduler();
        var machine = scheduler.CreateMachine(definition, traceBuffer);

        // Live transition logging
        machine.OnTransition += trace =>
        {
            var from = definition.NameLookup.GetStateName(trace.FromState);
            var to = definition.NameLookup.GetStateName(trace.ToState);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  ── transition: {from} → {to} | {trace.Reason}: {trace.Detail}");
            Console.ResetColor();
        };

        // Prepare events
        var tapEvent = MachineBuilder.EventIdFrom(definition, "UserTapped");
        var permEvent = MachineBuilder.EventIdFrom(definition, "PermissionResponse");
        var nudgeEvent = MachineBuilder.EventIdFrom(definition, "NudgeResponse");
        var prefsEvent = MachineBuilder.EventIdFrom(definition, "PreferencesSubmitted");

        Print("ENGINE", "Starting onboarding flow...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        machine.Start(time);
        scheduler.Update(time);

        while (machine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            if (machine.Status == MachineStatus.Blocked && machine.BlockReason == BlockKind.WaitForEvent)
            {
                // Simulate user interactions with appropriate delay
                time += 0.5f;

                var snap = machine.GetDebugSnapshot();
                var waitingEvent = snap.WaitingForEventName;

                Print("INPUT", $"Simulating event: {waitingEvent}", ConsoleColor.DarkGray);

                EventId eventToSend = waitingEvent switch
                {
                    "UserTapped" => tapEvent,
                    "PermissionResponse" => permEvent,
                    "NudgeResponse" => nudgeEvent,
                    "PreferencesSubmitted" => prefsEvent,
                    _ => throw new InvalidOperationException($"Unknown event: {waitingEvent}")
                };

                machine.SendEvent(eventToSend, time);
            }

            scheduler.Update(time);
        }

        // ── Debug snapshot and timeline ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("TIMELINE", "Transition history:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));

        var traces = traceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = definition.NameLookup.GetStateName(t.FromState);
            var to = definition.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail}) at t={t.Timestamp:F1}s");
        }

        Console.WriteLine(new string('═', 60));

        var finalSnap = machine.GetDebugSnapshot();
        Console.WriteLine();
        Print("RESULT", $"Machine: {finalSnap.MachineName} — {finalSnap.Status}");
        Print("RESULT", $"Final state: {finalSnap.CurrentStateName}");
        Print("RESULT", "No booleans. No coroutines. Full traceability.");
    }

    static void Print(string tag, string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{tag,-12}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}

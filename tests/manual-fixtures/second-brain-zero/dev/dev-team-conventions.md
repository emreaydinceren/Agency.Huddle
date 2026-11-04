# How this Development Team works

Fictional test data for a from-zero Huddle run.

## Facts about the repository

- The repository is local. It has **no remote**, so nothing is pushed.
- The default branch is `main`.
- The project targets **.NET 10**. Tests use **xUnit**.
- The repository root holds exactly one solution file, so `dotnet test` at the root runs every test.

## Ways of working

- Work on a branch named `feat/<short-name>` and merge it into `main` when the Task is verified.
- Commit messages start with a verb in the present tense, such as `Add quote calculator`.
- Money is held in `decimal`, never `double`.
- The app uses no NuGet package at run time. Only the test project has packages.
- The repository's `README.md` says how to run the app and how to run the tests.

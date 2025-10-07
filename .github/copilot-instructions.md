# Guidelines

- When making code changes, ensure that the code is kept as simple as possible. Don't add unnecessary complexity.
- When a code change is made, ensure that all relevant tests are updated or added to cover the new code.
- Ensure that code changes follow the existing coding style and conventions of the project.
- When adding a dependency, ensure that it is necessary and does not bloat the project. Additionally, ensure that the dependency is added to the appropriate project file such as the .csproj file for C# projects or package.json for Node.js projects.
- Do not hardcode secrets. Use environment variables or secure vaults to manage sensitive information.
- Don't put emojis in the code or markdown.
- Don't interrupt starting the UI with sleep commands.
- If a change is made to the API, ensure that any relevant UI components are also updated to reflect the changes, and vice versa.
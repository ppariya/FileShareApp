# File Sharing API

## Overview
This project is a file sharing service built with .NET and React. It provides a RESTful API for uploading, downloading, and managing files, along with a user-friendly web interface.

## Pre-requisites

It's recommended to use the provided Devcontainer for a consistent development environment. However, you can also set up the environment manually.

### Using Devcontainer
1. Ensure Docker is installed and running on your machine.
1. Open the project folder in VS Code and reopen it in the container when prompted.

### Not using Devcontainer
1. Ensure .NET (9.0) is installed. You can download it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
2. Ensure Node.js (22.20.0) is installed. You can download it from [here](https://nodejs.org/en/download/).
3. Ensure Bicep is installed. You can follow the instructions [here](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/install).
4. Ensure Azure CLI is installed. You can follow the instructions [here](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest#install).
5. Run `npm i -g react-scripts`

## How to Run

### API

1. `cd api/FileSharingApi`
1. `dotnet run`
1. The API will be available at [http://localhost:5109](http://localhost:5109)
1. Swagger UI is available at [http://localhost:5109/swagger](http://localhost:5109/swagger)

#### Run Tests
1. `cd api/FileSharingApi.Tests`
1. `dotnet test`

### UI

1. `cd ui`
1. `npm install`
1. `npm start`
1. Open your browser to [http://localhost:3000](http://localhost:3000) to use the UI.

#### Run Tests
1. `cd ui`
1. `npm test`

## How to Deploy

1. Login to Azure: `az login`
1. Deploy
    - For initial deployment: `./deploy/deploy.sh`
    - To update containers only: `./deploy/deploy.sh --containers-only`
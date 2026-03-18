# TalentKernel

TalentKernel is a **Semantic Kernel** based application designed to automate the search and analysis of job postings. The primary goal of this project is to gain hands-on experience with Artificial Intelligence development, LLM (Large Language Model) orchestration, and the implementation of technical plugins within the .NET ecosystem.

## Project Objectives

The core purpose of this project is to leverage the power of Large Language Models to perform **semantic searches**, moving beyond the limitations of traditional keyword-based platforms. 

Traditional job searches often rely on "exact text matches," which can be frustrating and inefficient. For example:
* **Nuanced Requirements:** A traditional search might miss a role offering **"Relocation Support"** if the recruiter used terms like "International hiring" or "Assistance with moving costs."
* **Tech Stack Synonyms:** Searching for ".NET" might exclude roles described with "C# Specialist" or "Core/Web API development" depending on the engine's rigidity.
* **Contextual Fit:** Traditional engines cannot easily filter for "Remote-first culture" or "Greenfield projects" unless those exact strings are present.

TalentKernel uses AI to understand the *intent* behind the job description, making the process less tedious and uncovering opportunities that a simple text match would otherwise overlook.

## Project Structure

The solution is divided into two main projects:

* **TalentKernel (Class Library):** Contains the core logic and agent capabilities.
    * **Models:** Data definitions for candidate profiles, job opportunities, and analytical results.
    * **Plugins:** A set of specialized tools (`JobSearchPlugin`, `JobAnalystPlugin`, `ApplicationArchitectPlugin`, etc.) that allow the agent to interact with external APIs and process technical information in a structured manner.
* **TalentKernelChat (Console Application):** The entry point of the application. It manages configuration, Kernel construction, and real-time user interaction.

## Prerequisites

To run this project, you will need:
1. An **Azure AI** subscription with a deployed model (Phi-4 or similar recommended).
2. An **Adzuna** developer account to access their job search API.

## Setup

To ensure the application functions correctly, you must manually populate the `appsettings.json` file in the **TalentKernelChat** project. Use the following structure:

```json
{
  "AzureAi": {
    "DeploymentName": "YOUR_DEPLOYMENT_NAME",
    "Endpoint": "YOUR_ENDPOINT_URL",
    "ApiKey": "YOUR_AZURE_API_KEY"
  },
  "Adzuna": {
    "AppId": "YOUR_ADZUNA_APP_ID",
    "ApiKey": "YOUR_ADZUNA_API_KEY"
  }
}

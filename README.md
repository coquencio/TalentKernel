# 🤖 TalentKernel

TalentKernel is a **Semantic Kernel** based application designed to automate the search and analysis of job postings using AI agents. This project demonstrates advanced LLM orchestration, agentic workflows, and custom plugin development within the **.NET 9** ecosystem.

## 🎯 Project Objectives

The core purpose of this project is to leverage Large Language Models to perform **semantic searches**, moving beyond the limitations of traditional keyword-based platforms. 

Instead of rigid text matching, TalentKernel understands the *intent* and *context* of job descriptions:
* **🔍 Nuanced Requirements:** Identifies "Relocation Support" even if the specific string isn't present.
* **💻 Tech Stack Synonyms:** Connects ".NET" with "C# Specialist" or "Web API development" naturally.
* **🌍 Contextual Fit:** Filters for "Remote-first" or "Greenfield projects" by analyzing the job's overall description.

## 🏗️ Project Structure & Interface

The application has transitioned from a basic console loop to a **Discord Bot**, providing a modern, persistent, and multi-modal interface for career interaction.

* **🧠 Core Logic:** A library containing specialized plugins:
    * `JobSearchPlugin`: Connects to recruitment APIs (Adzuna).
    * `JobAnalystPlugin`: Evaluates job requirements against candidate profiles.
    * `FileExtractorPlugin`: Uses **PdfPig** to parse CVs and job descriptions from PDF attachments.
    * `MarkdownBatchReaderPlugin`: Scrapes and cleans web-based job postings.
* **💬 Discord Integration:** A `BackgroundService` that allows the agent to live in a Discord server, supporting file uploads (CVs) and rich text responses.

## ⚙️ Prerequisites

To run this project, you will need:
1. An **Azure AI** subscription with a deployed model (**DeepSeek V3.2** or GPT-4o recommended).
2. An **Adzuna** developer account for job search data.
3. A **Discord Bot Token** from the [Discord Developer Portal](https://discord.com/developers/applications).

## 🛠️ Setup

Populate the `appsettings.json` file in the main project. Ensure the **Message Content Intent** is enabled in your Discord Developer Portal settings.

```json
{
  "Model": {
    "key": "YOUR_AZURE_API_KEY",
    "deploymentName": "YOUR_DEPLOYMENT_NAME",
    "endpoint": "YOUR_ENDPOINT_URL"
  },
  "Adzuna": {
    "AppId": "YOUR_ADZUNA_APP_ID",
    "ApiKey": "YOUR_ADZUNA_API_KEY"
  },
  "Discord": {
    "Token": "YOUR_DISCORD_BOT_TOKEN"
  }
}
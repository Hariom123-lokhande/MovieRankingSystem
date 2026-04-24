# 🎬 Movie Search Ranking System

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/apps/aspnet)
[![ML.NET](https://img.shields.io/badge/ML.NET-3.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/apps/machinelearning-ai/ml-dotnet)
[![JWT](https://img.shields.io/badge/Authentication-JWT-green?logo=json-web-tokens&logoColor=white)](https://jwt.io/)
[![UI](https://img.shields.io/badge/UI-Glassmorphism-blue)](https://css-tricks.com/glassmorphism-creative-compliancy-and-accessibility/)


## Executive Summary
The Movie Search Ranking System is a sophisticated information retrieval platform designed to demonstrate the power of Machine Learning (ML.NET) in optimizing search result relevance. In standard search implementations, results are typically sorted by a single static attribute (e.g., date or average rating). This system advances that paradigm by using supervised learning models to predict the optimal order of results based on multi-dimensional feature sets.

The platform provides a comprehensive environment to train, evaluate, and compare three industry-standard ranking strategies: **Listwise**, **Pointwise**, and **Pairwise**.

---

## Core Objectives
*   **Relevance Optimization**: Transition from simple keyword filtering to intelligent result ranking.
*   **Algorithmic Comparison**: Provide a side-by-side analysis of different ranking methodologies.
*   **Production Readiness**: Implement a high-performance, asynchronous Web API backend capable of sub-millisecond inference.
*   **User Transparency**: Deliver clear insights into "why" a specific ranking was assigned through visual metadata and explanation tags.

---

## 🔐 User Authentication & Security
The system now includes a robust security layer to protect sensitive ranking and training operations:
*   **JWT-Based Authorization**: Stateless and secure communication between the frontend and Web API.
*   **Identity Management**: Full Login and Signup workflow with secure password hashing.
*   **Audit Fields**: Automatic tracking of user activity (`CreatedAt`, `UpdatedAt`, `LastLogin`).
*   **Protected Endpoints**: Core ranking APIs are restricted to authenticated users only.


---

## Ranking Strategies Explained

### 1. Listwise Ranking (State-of-the-Art)
The Listwise approach treats the entire set of search results as a single entity during training. It uses the **LambdaMART (LightGBM)** algorithm to optimize for **NDCG** (Normalized Discounted Cumulative Gain).
*   **Best For**: Complex queries where the relative order of the entire list is more important than individual scores.
*   **Advantage**: Captures the global structure of the result set.

### 2. Pointwise Ranking (Regression-Based)
Pointwise ranking treats each movie independently. The model predicts a specific relevance score (0.0 to 3.0) for a given query-movie pair.
*   **Algorithm**: FastForest / LightGBM Regression.
*   **Best For**: High-scale environments requiring extremely low latency.

### 3. Pairwise Ranking (Tournament Style)
The Pairwise approach transforms the ranking problem into a series of binary classifications. The model is trained to decide which of two movies is more relevant for a specific query.
*   **Algorithm**: LightGBM Ranker.
*   **Mechanism**: A tournament-style inference loop calculates winning probabilities across all potential pairs to determine the final rank.

---

## Visual Documentation

### Primary Dashboard
The main interface features a modern glassmorphism design, allowing users to execute queries and switch between ML strategies in real-time.
![System Dashboard](Screenshots/Screenshot%202026-04-23%20184115.png)

### Strategy Analysis
Individual result cards provide deep insights into model signals, rank shifts, and relevance categories.
![Strategy Analysis](Screenshots/Screenshot%202026-04-23%20192957.png)

### Comparative Evaluation (Baseline vs. ML)
A dedicated interface for comparing standard database sorting (Baseline) against ML-driven ranking. This view highlights the "Rank Shift" (how many places a movie moved due to intelligence).
![Comparative Evaluation](Screenshots/Screenshot%202026-04-23%20193029.png)

---

## Technical Architecture

### 1. Data & Feature Engineering
The system utilizes an augmented dataset. During the ETL process, several engineered features are generated:
*   **Normalized Popularity**: Rescaling raw rating counts for model stability.
*   **Rating-Popularity Blend**: A derived feature capturing the intersection of quality and traction.
*   **High-Rating Indicator**: A boolean signal for elite-tier content.

### 2. Backend Services
*   **Prediction Service**: A thread-safe singleton service that manages MLContext and pre-loads trained models for high-concurrency support.
*   **Ranking Service**: Orchestrates the training pipelines and algorithm selection.
*   **Evaluation Service**: Computes precision metrics like NDCG and MAP (Mean Average Precision) to validate model performance.

### 3. API Infrastructure
Built on **ASP.NET Core 8.0**, the API exposes RESTful endpoints for:
*   `POST /api/rank`: Single-mode ranking inference.
*   `POST /api/compareall`: Multi-strategy cross-comparison.
*   `POST /api/train`: Real-time model retraining.
*   `POST /api/account/login`: Secure authentication and JWT issuance.
*   `POST /api/account/signup`: New user registration.


---

## Performance & Scalability
*   **Model Caching**: Models are serialized as `.zip` files and loaded into memory as `PredictionEngine` pools.
*   **Data Tier**: In-memory caching of the primary dataset in the `MovieDataLoader` minimizes disk I/O latency.
*   **UI Performance**: Zero-dependency Vanilla JavaScript frontend ensures rapid DOM updates and minimal bundle size.

---

---

## 🚦 Getting Started

1.  **Database Setup**: Update the `DefaultConnection` in `appsettings.json` to point to your SQL Server.
2.  **Apply Migrations**:
    ```bash
    dotnet ef database update
    ```
3.  **Run Application**: Launch via Visual Studio or `dotnet run`.
4.  **Access**: Navigate to the Login page to create your account and start ranking.

---

*Developed by Hariom Lokhande*


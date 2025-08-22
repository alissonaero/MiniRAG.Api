# MiniRAG API

**Short description**
MiniRAG is a small Retrieval-Augmented Generation (RAG) API implemented in C#/.NET. It ingests documents, breaks them into chunks, generates embeddings (via a separate embeddings service backed by Hugging Face sentence-transformers), stores vectors and metadata in Weaviate, and uses a local LLM inference service to answer user queries using retrieved context.

This repository contains the API server that orchestrates the RAG pipeline: ingest → embed → store → retrieve → generate.

---

## Key components
- **API (C# / .NET)** — Controllers, Services and Clients that implement the ingestion, search and generation flows.
- **Embeddings service (external)** — expects a service (e.g. FastAPI) that uses Hugging Face Hub + sentence-transformers to produce embeddings. Default URL is `http://localhost:5000`.
- **Weaviate (vector DB)** — stores document chunks and vectors. Default URL is `http://localhost:8080`.
- **Local LLM inference server** — the project calls a local LLM server to generate answers. Default URL in `appsettings.json` is `http://localhost:11434` and example model `llama3.2:3b`.

---

## Features
- Document ingestion with chunking and optional overlap
- Embeddings generation via an external service (Hugging Face sentence-transformers)
- Vector storage and similarity search with Weaviate
- Prompt composition and generation with a local LLM
- Administrative endpoints: create Weaviate class, clear data, inspect stored objects
- Simple token-budget-aware design notes (TODO: dynamic `max_tokens` implementation)

---

## Architecture / Flow
1. **Ingest**: POST a document -> the API splits it into chunks and sends chunks to the Embeddings service.
2. **Embed**: Embeddings service returns float vectors for each chunk.
3. **Store**: The API stores chunks + metadata in Weaviate with the vector attached.
4. **Query**: On user query, API generates an embedding for the question, queries Weaviate for the top-K similar chunks, composes a prompt (system + context + question) and calls the LLM server to produce a response.

---

## Getting started — Requirements
- .NET SDK (6.0 or later recommended) to build/run the API
- Docker & Docker Compose (to run Weaviate locally and other helper services)
- An embeddings service (Hugging Face sentence-transformers based) running and reachable
- A local LLM inference server (or a compatible HTTP endpoint) reachable from the API

> Note: This repository expects the external services to be available (Weaviate, embeddings server, LLM server). Example dev docker-compose snippets are shown below.

---

## Example `appsettings.json` (important keys)
```json
{
  "Embeddings": { "Url": "http://localhost:5000" },
  "Weaviate": { "Url": "http://localhost:8080", "ClassName": "DocumentChunk" },
  "LLM": { "Url": "http://localhost:11434", "ModelName": "llama3.2:3b", "ContextSize": 4096 }
}
```

---

## Example Docker Compose (dev)
Below is a minimal development example — adjust versions and settings to your environment.

```yaml
version: '3.8'
services:
  weaviate:
    image: semitechnologies/weaviate:latest
    ports:
      - "8080:8080"
    environment:
      - QUERY_DEFAULTS_LIMIT=20
      - AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED=true
      - PERSISTENCE_DATA_PATH=/var/lib/weaviate
    volumes:
      - ./weaviate_data:/var/lib/weaviate

  redis:
    image: redis:7
    ports:
      - "6379:6379"

  # (Embeddings service and LLM server are expected to be run separately)

  api:
    build: ./api
    ports:
      - "5002:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - weaviate
      - redis
```

---

## Important Endpoints (examples)
> Replace host/port with those configured in `appsettings.json`.

- `POST /api/rag/ingest` — Ingest a document (API will chunk, embed and store). Example body: `{ "source": "doc1", "text": "...long text..." }`.
- `POST /api/rag/query` — Query the knowledge base. Example body: `{ "question": "What is the delivery time?", "topK": 5 }`.
- `POST /api/rag/create-class` — Creates the `DocumentChunk` class in Weaviate schema (admin).
- `POST /api/rag/clear-by-source` — Delete objects by `source` prefix.

Check the Controllers folder for exact signatures and more endpoints.

---

## Configuration & Tuning notes
- **Chunking**: choose chunk sizes (in tokens or characters) and overlap to balance granularity vs. context. Typical chunk sizes: 100–400 tokens with 20–30% overlap.
- **topK**: controls how many chunks the API requests from Weaviate. `topK` is often 3–10 in practice; you can retrieve more candidates and then assemble a token-aware context.
- **Token budgeting**: to avoid exceeding the LLM's context window, calculate prompt token usage and compute `max_tokens` dynamically. There is a TODO placeholder in the code to implement this logic.
- **Embedding model**: the embeddings server should return consistent vector dimensionality. If the embedding model changes (dimension change), reindex the corpus.

---

## Tokenizer microservice (recommended)
For exact token counting (required for precise token budgeting), run a small tokenizer service (e.g., FastAPI + Hugging Face `tokenizers` or `tiktoken`) in a container. The API can call this service to compute exact token counts for the assembled prompt before calling the LLM.

(There is a short TODO comment in the code to implement dynamic `max_tokens` using such a tokenizer service.)

---

## Troubleshooting
- **Embeddings API returns no vectors**: check embeddings service logs, model availability and the correct endpoint route.
- **Weaviate errors (4xx/5xx)**: verify the Weaviate URL, schema and network connectivity; consult Weaviate logs.
- **LLM responses truncated or too short**: implement token counting and dynamic `max_tokens`; adjust `ReservedForAnswer`.
- **Performance**: use batch embedding, Redis caching of embeddings, and a re-ranker to reduce LLM context noise.

---

## Development & Testing
- Unit tests: mock external HTTP clients (embeddings, weaviate, llm) for services.
- Integration tests: spin up a local Weaviate and a lightweight embeddings server to run end-to-end tests.
- Load tests: simulate bulk ingestion and query workloads to validate batching and Weaviate sizing.

---

## Contributing
Contributions welcome. Suggested tasks:
- implement tokenizer microservice + dynamic max_tokens logic
- token-aware chunk assembly
- re-ranker using a cross-encoder for better top-K selection
- robust CI that runs integration tests with a local Weaviate container

---

## LICENSE
This repository is provided as-is. Add a license file if you want to open-source it publicly.

---

## Contact / Author
Project originally developed by the repository owner with assistance from multiple AI tools. For questions, open an issue in the repo.


# 🏀 SamàBot: AI-Powered Assistant for Club Bàsquet Samà

Built with a **"Quality-First"** mindset by a former QA turned Backend Engineer.

> "I spent 7 years learning how software breaks so I could spend the rest of my career building systems that don't."

---

## 🚀 Overview
**SamàBot** is an advanced AI assistant designed to handle summer camp logistics, schedules, menus, and general inquiries for **Club Bàsquet Samà** via WhatsApp. 

This project isn't just a bot; it's a showcase of **Modern .NET Engineering**. It moves away from traditional CRUD/REST patterns toward a highly resilient, event-driven architecture that prioritizes data integrity and developer experience.

## 🛠️ The Tech Stack
Built with the cutting-edge **.NET 10** (Preview) and **C# 14**, SamàBot leverages the "Critter Stack":

- **Marten**: Document DB and Event Store on top of PostgreSQL. Every interaction is recorded as a domain event, ensuring a perfect audit trail.
- **Wolverine**: The next-gen "Message Bus" and "Mediator". It handles complex asynchronicity with elegant cascading messages and outbox patterns.
- **Microsoft.Extensions.AI**: A unified, provider-agnostic SDK for LLM integration (Ollama, OpenAI, Gemini).
- **pgvector**: Integrated directly into Marten for lightning-fast RAG (Retrieval-Augmented Generation). By feeding the AI with **verified official club documentation**, we ensure the bot provides factual, accurate information while strictly preventing AI hallucinations—a clinical application of the "Quality-First" philosophy.

## 🛡️ Quality as a First-Class Citizen
Coming from a QA background, I believe that **untested code is legacy code**.
- **Alba**: For full-stack, in-memory component testing of the entire HTTP/Wolverine pipeline.
- **Testcontainers**: To guarantee that integration tests run against real, ephemeral PostgreSQL instances in Docker—no mocks, no "it works on my machine."
- **Vertical Slice Architecture**: Features are organized by business value, not by technical layers (Controllers/Services/Repos), reducing cognitive load and making the system easier to evolve.

## 🏗️ Architecture: The Event Pipeline
SamàBot operates on a reactor-like event pipeline:
1. **Webhook Intake**: Validates WhatsApp HMAC signatures and persists the raw intake.
2. **NLP Detection**: Uses AI to identify language (Catalan, Spanish, English) and user intent.
3. **RAG Retrieval**: Vector searches PDFs to find contextually relevant club information.
4. **Persona Generation**: Crafts a response using the "Vilanoví" assistant persona.
5. **Dispatch**: Safely sends the response back to the user via the Meta Cloud API.

---

*This project is built under strict AI-driven coding conventions documented in the global `.dotnetrules` of the workspace.*

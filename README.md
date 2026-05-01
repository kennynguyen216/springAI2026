Alfred is a **privacy-first, local AI email assistant**. It transforms your Gmail inbox into a structured dashboard where emails are categorized and summarized locally using **Llama 3**.

### Members
* **Lily Yang**
* **Cameron Bly**
* **Kennedy Nyugen**
* **Jayden Hong**

---

## About Our Software

By utilizing local LLMs, Alfred ensures that sensitive data—like bank details or SSNs—never leaves your machine. The built-in **Privacy Gate** acts as a final filter, flagging sensitive content before the AI even sees it.

### Key Features
* **Local Processing:** Powered by the Ollama engine to keep data private.
* **Smart Categorization:** Automatically organizes your Gmail inbox.
* **Security First:** Built-in detection for sensitive information.
* **Interactive Dashboard:** Built with Streamlit for a clean, user-friendly interface.

---

## Getting Started

Follow these steps to initialize your local environment and launch the assistant.

### 1. Clone the repository
```bash
git clone [https://github.com/ly9247601/Alfred-Email-Assistant.git](https://github.com/ly9247601/Alfred-Email-Assistant.git)
cd Alfred-Email-Assistant

###2. Install packages
python -m pip install streamlit streamlit-calendar
python -m pip install ollama torchvision
python -m pip install google-auth-oauthlib google-api-python-client

###3. Run Code
   streamlit run app.py

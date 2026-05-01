from google_utils import get_gmail_service
from privacy_gate import PrivacyGate
from summarizer import get_email_insight

def fetch_emails(service, count=5):
    results = service.users().messages().list(userId='me', maxResults=count).execute()
    messages = results.get('messages', [])
    
    email_data = []
    
    for msg in messages:
        full_msg = service.users().messages().get(userId='me', id=msg['id']).execute()
        
        snippet = full_msg.get('snippet', '')
        email_data.append(snippet)
        
    return email_data

def run_pipeline():
    gate = PrivacyGate()
    service = get_gmail_service()
    
    emails = fetch_emails(service)
    
    for content in emails:
        print(f"\nScanning: {content[:50]}...")
        
        if gate.check_email(content):
            print("✅ SAFE: Analyzing with Local Llama 3...")
            insight = get_email_insight(content)
            print(f"AI INSIGHT:\n{insight}")
        else:
            print("🔒 SENSITIVE: Staying on device. No AI summary generated.")
        print("-" * 40)

if __name__ == "__main__":
    run_pipeline()
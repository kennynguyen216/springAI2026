import os.path
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build


SCOPES = [
    'https://www.googleapis.com/auth/gmail.readonly'
]

def get_gmail_service():
    creds = None
    if os.path.exists('token.json'):
        creds = Credentials.from_authorized_user_file('token.json', SCOPES)
    
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(
                'credentials.json', SCOPES)
            creds = flow.run_local_server(port=0)
        
        with open('token.json', 'w') as token:
            token.write(creds.to_json())

    return build('gmail', 'v1', credentials=creds)

if __name__ == "__main__":
    try:
        service = get_gmail_service()
        print("✅ Successfully connected to Gmail!")
        
        results = service.users().labels().list(userId='me').execute()
        labels = results.get('labels', [])
        print(f"Found {len(labels)} Gmail labels.")
        
    except Exception as e:
        print(f"❌ Error: {e}")
        print("Check if credentials.json is in the correct folder.")
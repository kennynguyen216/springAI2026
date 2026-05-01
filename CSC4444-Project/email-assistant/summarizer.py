import ollama
import json
import re

def get_email_insight(email_text):
    prompt = f"""
    You are Alfred, a digital butler. Categorize this email into EXACTLY one of these four categories:
    1. Important: Personal emails, direct alerts, or school/work tasks.
    2. Newsletter: Marketing, ads, brand updates, and retail offers.
    3. Spam: Suspicious prize offers, random warehouse alerts, or unsolicited junk.
    4. Private: Highly sensitive personal info or account recovery.

    EMAIL CONTENT: {email_text}

    Return ONLY a JSON object:
    {{
      "category": "Important/Newsletter/Spam/Private",
      "summary": "1-sentence summary",
      "event_name": "None",
      "deadline": "None"
    }}
    """
    
    try:
        response = ollama.generate(model='llama3', prompt=prompt)
        text = response['response'].strip()
        
        match = re.search(r'\{.*\}', text, re.DOTALL)
        if not match:
            raise ValueError("No JSON braces found in response")
            
        json_str = match.group(0)
        
        json_str = json_str.replace('```json', '').replace('```', '').strip()
        
        return json.loads(json_str)

    except Exception as e:
        return {
            "summary": "Unable to generate summary at this time.",
            "category": "Newsletter",
            "deadline": None,
            "event_name": None
        }
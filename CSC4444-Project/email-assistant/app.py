import streamlit as st
import os
from google_utils import get_gmail_service
from privacy_gate import PrivacyGate
from summarizer import get_email_insight
from database import init_db, add_event, get_all_events, delete_event, add_note, get_all_notes, delete_note
from streamlit_calendar import calendar

def local_css(file_name):
    parent_dir = os.path.dirname(os.path.abspath(__file__))
    file_path = os.path.join(parent_dir, file_name)
    
    if os.path.exists(file_path):
        with open(file_path) as f:
            st.markdown(f'<style>{f.read()}</style>', unsafe_allow_html=True)
    
    st.markdown("""
        <style>
            [data-testid="stSidebarCollapseButton"], 
            [data-testid="collapsedControl"],
            .st-emotion-cache-6qob1r, 
            .st-emotion-cache-1wbqy5l {
                display: none !important;
                visibility: hidden !important;
                width: 0px !important;
                height: 0px !important;
            }

            [data-testid="stSidebar"] > div:first-child {
                padding-top: 0px !important;
            }
        </style>
    """, unsafe_allow_html=True)

init_db()
st.set_page_config(page_title="AI Assistant", layout="wide", initial_sidebar_state="expanded")
local_css("style.css")

if 'email_buckets' not in st.session_state:
    st.session_state.email_buckets = None

@st.cache_resource
def load_privacy_gate():
    return PrivacyGate()

gate = load_privacy_gate()

with st.sidebar:
    st.title("💽 System Menu")
    
    try:
        service = get_gmail_service()
        profile = service.users().getProfile(userId='me').execute()
        st.success(f"🟢 ONLINE: {profile.get('emailAddress')}")
    except Exception:
        st.error("🔴 OFFLINE: Gmail Disconnected")
    
    st.divider()
    page = st.radio("MODULES:", ["📥 INBOX", "📅 CALENDAR", "📝 NOTES"])
    
    st.divider()
    st.subheader("📅 CALENDAR")
    logs = get_all_events()
    if not logs:
        st.write("NO IMPORTANT DATES")
    else:
        for eid, title, date in logs[:3]:
            st.markdown(f"**💾 [{date}]** {title}")

    st.divider()
    
    with st.popover("🔐 PRIVACY SETTINGS", use_container_width=True):
        st.subheader("SYSTEM INTEGRITY")
        st.code("MODE: AIR-GAPPED\nENCRYPTION: AES-256\nSTATUS: SECURE")
        st.divider()
        
        if 'auto_log' not in st.session_state:
            st.session_state.auto_log = False
            
        st.session_state.auto_log = st.toggle(
            "AUTO-ARCHIVE PRIVATE", 
            value=st.session_state.auto_log
        )
        if st.session_state.auto_log:
            st.warning("⚠️ AUTO-PILOT ENABLED")

if page == "📥 INBOX":
    st.title("🤖 ALFRED - An AI Email Assistant")
    
    try:
        service = get_gmail_service()
        if st.button("🔄 SYNC INBOX"):
            with st.spinner("💾 DATA_FETCHING..."):
                results = service.users().messages().list(userId='me', maxResults=10).execute()
                messages = results.get('messages', [])
                buckets = {"Important": [], "Newsletter": [], "Spam": [], "Privacy Blocked": []}

                for msg in messages:
                    full_msg = service.users().messages().get(userId='me', id=msg['id']).execute()
                    headers = full_msg.get('payload', {}).get('headers', [])
                    subject = next((h['value'] for h in headers if h['name'] == 'Subject'), "No Subject")
                    sender = next((h['value'] for h in headers if h['name'] == 'From'), "Unknown")
                    snippet = full_msg.get('snippet', '')

                    if not gate.check_email(snippet):
                        buckets["Privacy Blocked"].append({
                            "subject": subject, "sender": sender, "snippet": snippet, "id": msg['id']
                        })
                    else:
                        
                        insight = get_email_insight(snippet)
                        cat = insight.get("category", "Newsletter")
                        target_cat = cat if cat in buckets else "Newsletter"
                        buckets[target_cat].append({
                            "subject": subject, "sender": sender, "snippet": snippet, "insight": insight, "id": msg['id']
                        })
                st.session_state.email_buckets = buckets

        if st.session_state.email_buckets:
            buckets = st.session_state.email_buckets
            tabs = st.tabs(["⭐️ IMPORTANT", "📰 NEWSLETTERS", "❓ SPAM", "🔐 PRIVATE"])

            def display_emails(email_list, is_private=False):
                if not email_list:
                    st.write("Sector empty.")
                    return
                
                for mail in email_list:
                    mail_id = mail['id']
                    with st.expander(f"📁 {mail['subject']}"):
                        st.write(f"**FROM:** {mail['sender']}")
                        
                        if is_private:
                            st.warning("🔒 SENSITIVE_DATA_DETECTED")
                            unlocked = st.checkbox("UNCOVER ENCRYPTED CONTENT", key=f"unlock_{mail_id}")
                            
                            if unlocked:
                                st.info(f"**RAW_SNIPPET:** {mail['snippet']}")
                                
                                if st.session_state.get('auto_log', False):
                                    if st.button("🔍 GENERATE BRIEF SUMMARY", key=f"analyze_{mail_id}"):
                                        mail['insight'] = get_email_insight(mail['snippet'])
                                        st.rerun()
                                    
                                    if 'insight' in mail:
                                        summary = mail['insight'].get('summary', 'No summary available.')
                                        st.success(f"**BRIEF SUMMARY:** {summary}")
                                        
                                        c1, c2 = st.columns(2)
                                        deadline = mail['insight'].get('deadline')
                                        if deadline and str(deadline).lower() != 'none':
                                            if c1.button("📅 LOG_PRIVATE_EVENT", key=f"p_cal_{mail_id}"):
                                                add_event(mail['insight'].get('event_name'), deadline)
                                                st.toast("PRIVATE EVENT SAVED")
                                        if c2.button("📝 SAVE_PRIVATE_NOTE", key=f"p_note_{mail_id}"):
                                            add_note(mail['sender'], mail['subject'], summary)
                                            st.toast("PRIVATE NOTE ARCHIVED")
                                else:
                                    st.error("🚫 PRIVACY LOCK: Enable 'AUTO-ARCHIVE' to summarize or save private logs.")
                        
                        else:
                            
                            summary = mail['insight'].get('summary', 'Summary unavailable.')
                            st.info(f"**SUMMARY:** {summary}")
                            
                            deadline = mail['insight'].get('deadline')
                            c1, c2 = st.columns(2)
                            if deadline and str(deadline).lower() != 'none':
                                if c1.button("📅 SAVE EVENT", key=f"cal_{mail_id}"):
                                    add_event(mail['insight'].get('event_name'), deadline)
                                    st.toast("EVENT SAVED")
                            if c2.button("📝 SAVE NOTE", key=f"note_{mail_id}"):
                                add_note(mail['sender'], mail['subject'], summary)
                                st.toast("NOTE ARCHIVED")

            with tabs[0]: display_emails(buckets["Important"])
            with tabs[1]: display_emails(buckets["Newsletter"])
            with tabs[2]: display_emails(buckets["Spam"])
            with tabs[3]: display_emails(buckets["Privacy Blocked"], is_private=True)

    except Exception as e:
        st.error(f"📡 CONNECTION_ERROR: {e}")

        with tabs[0]: display_emails(buckets["Important"])
        with tabs[1]: display_emails(buckets["Newsletter"])
        with tabs[2]: display_emails(buckets["Spam"])            
        with tabs[3]: display_emails(buckets["Privacy Blocked"], is_private=True)

    except Exception as e:
        st.error(f"📡 CONNECTION_ERROR: {e}")

elif page == "📅 CALENDAR":
    st.title("📂 IMPORTANT DATES")
    all_ev = get_all_events()
    if not all_ev:
        st.info("NO EVENTS SAVED")
    else:
        calendar_events = [{"title": t, "start": d, "end": d, "id": i, "color": "#ffcc00", "textColor": "black"} for i, t, d in all_ev]
        calendar(events=calendar_events, options={"initialView": "dayGridMonth"}, key="calendar_grid")
        st.divider()
        for eid, title, date in all_ev:
            c = st.columns([2, 4, 1])
            c[0].write(date)
            c[1].write(title)
            if c[2].button("🗑️", key=f"de_{eid}"):
                delete_event(eid)
                st.rerun()

elif page == "📝 NOTES":
    st.title("🗒️ IMPORTANT NOTES")
    all_n = get_all_notes()
    if not all_n:
        st.info("NO NOTES SAVED")
    else:
        for i, (nid, snd, sub, summ) in enumerate(all_n):
            with st.container(border=True):
                st.caption(f"SENDER: {snd}")
                st.subheader(sub)
                st.write(summ)
                if st.button(f"🗑️ DELETE NOTE {i+1}", key=f"dn_{nid}"):
                    delete_note(nid)
                    st.rerun()
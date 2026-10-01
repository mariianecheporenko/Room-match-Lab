import json

def make_exec(code):
    return code.split('\n')

def make_event(scripts):
    events = []
    for s in scripts:
        listen = 'test' if s['type'] == 'afterResponse' else 'prerequest'
        events.append({
            'listen': listen,
            'script': {
                'type': 'text/javascript',
                'exec': make_exec(s['code'])
            }
        })
    return events

def make_url(url_str, query_params=None):
    obj = {'raw': url_str}
    if query_params:
        obj['query'] = [{'key': k, 'value': v} for k, v in query_params]
    return obj

def make_body(body):
    if not body:
        return None
    if body['type'] == 'json':
        return {'mode': 'raw', 'raw': body['content'], 'options': {'raw': {'language': 'json'}}}
    elif body['type'] == 'urlencoded':
        return {'mode': 'urlencoded', 'urlencoded': body['content']}
    elif body['type'] == 'formdata':
        return {'mode': 'formdata', 'formdata': body['content']}
    return None

requests_data = [
    {
        'name': 'Create Profile',
        'order': 1000,
        'method': 'POST',
        'url': '{{baseUrl}}/api/Profiles',
        'body': {
            'type': 'json',
            'content': '{\n  "fullName": "Student Test User",\n  "email": "student.test.{{$timestamp}}@knu.ua",\n  "avatarUrl": "https://example.com/avatar.jpg",\n  "budget": 9000,\n  "cleanliness": 4,\n  "sleepSchedule": 3,\n  "partyTolerance": 2,\n  "isSmoker": false,\n  "hasOwnPets": false,\n  "petTolerance": 1\n}'
        },
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 201 Created", function () {\n    pm.expect(pm.response.code).to.eql(201);\n});\n\npm.test("Profile created and ID returned", function () {\n    var data = pm.response.json();\n    pm.expect(data.id).to.not.be.empty;\n    pm.collectionVariables.set("profileId", data.id);\n});'}]
    },
    {
        'name': 'Get Profile',
        'order': 2000,
        'method': 'GET',
        'url': '{{baseUrl}}/api/Profiles/{{profileId}}',
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 200 OK", function () {\n    pm.expect(pm.response.code).to.eql(200);\n});\n\npm.test("ID matches saved profile", function () {\n    var data = pm.response.json();\n    pm.expect(data.id).to.eql(pm.collectionVariables.get("profileId"));\n});'}]
    },
    {
        'name': 'Create Housing',
        'order': 4000,
        'method': 'POST',
        'url': '{{baseUrl}}/api/Housings',
        'body': {
            'type': 'json',
            'content': '{\n  "title": "Cozy Student Apartment near Campus",\n  "pricePerMonth": 8000,\n  "city": "Kyiv",\n  "street": "Zoii Butenko",\n  "buildingNumber": "55",\n  "district": "Holosiivskyi",\n  "latitude": 50.3845,\n  "longitude": 30.4851,\n  "allowsSmoking": false,\n  "petPolicy": 1,\n  "requiredCleanliness": 4,\n  "requiredSleepSchedule": 3,\n  "requiredPartyTolerance": 2\n}'
        },
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 201 Created", function () {\n    pm.expect(pm.response.code).to.eql(201);\n});\n\npm.test("Housing created and ID returned", function () {\n    var data = pm.response.json();\n    pm.expect(data.id).to.not.be.empty;\n    pm.collectionVariables.set("housingId", data.id);\n});'}]
    },
    {
        'name': 'Non-existing housing',
        'order': 5000,
        'method': 'GET',
        'url': '{{baseUrl}}/api/Housings/00000000-0000-0000-0000-000000000000',
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 404 Not Found", function () {\n    pm.expect(pm.response.code).to.eql(404);\n});'}]
    },
    {
        'name': 'Get Housings with Pagination',
        'order': 6000,
        'method': 'GET',
        'url': '{{baseUrl}}/api/Housings',
        'query_params': [('skip', '0'), ('limit', '5')],
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 200 OK", function () {\n    pm.expect(pm.response.code).to.eql(200);\n});\n\npm.test("Response time is under 500ms", function () {\n    pm.expect(pm.response.responseTime).to.be.below(500);\n});\n\npm.test("Returns array of listings", function () {\n    var data = pm.response.json();\n    pm.expect(data).to.be.an("array");\n    pm.expect(data.length).to.be.at.most(5);\n});'}]
    },
    {
        'name': 'Create Booking Request & Calculate Match',
        'order': 7000,
        'method': 'POST',
        'url': '{{baseUrl}}/api/BookingRequests',
        'body': {
            'type': 'json',
            'content': '{\n  "profileId": "{{profileId}}",\n  "housingId": "{{housingId}}"\n}'
        },
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 201 Created", function () {\n    pm.expect(pm.response.code).to.eql(201);\n});\n\npm.test("Compatibility match score calculated properly", function () {\n    var data = pm.response.json();\n    pm.expect(data.matchScore).to.be.a("number");\n    pm.expect(data.matchScore).to.be.within(0, 100);\n    pm.expect(data.status).to.eql("Pending");\n});'}]
    },
    {
        'name': 'Profile non-valid scale',
        'order': 8000,
        'method': 'POST',
        'url': '{{baseUrl}}/api/Profiles',
        'body': {
            'type': 'json',
            'content': '{\n  "fullName": "Student Test User",\n  "email": "student.test.{{$timestamp}}@knu.ua",\n  "avatarUrl": "https://example.com/avatar.jpg",\n  "budget": 9000,\n  "cleanliness": 10,\n  "sleepSchedule": 3,\n  "partyTolerance": 2,\n  "isSmoker": false,\n  "hasOwnPets": false,\n  "petTolerance": 1\n}'
        },
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 400 Bad Request", function () {\n    pm.expect(pm.response.code).to.eql(400);\n});'}]
    },
    {
        'name': 'Profile negative budget',
        'order': 9000,
        'method': 'POST',
        'url': '{{baseUrl}}/api/Profiles',
        'body': {
            'type': 'json',
            'content': '{\n  "fullName": "Student Test User",\n  "email": "student.test.{{$timestamp}}@knu.ua",\n  "avatarUrl": "https://example.com/avatar.jpg",\n  "budget": -9000,\n  "cleanliness": 4,\n  "sleepSchedule": 3,\n  "partyTolerance": 2,\n  "isSmoker": false,\n  "hasOwnPets": false,\n  "petTolerance": 1\n}'
        },
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 400 Bad Request", function () {\n    pm.expect(pm.response.code).to.eql(400);\n});'}]
    },
    {
        'name': 'Delete Housing',
        'order': 1215247881869739,
        'method': 'DELETE',
        'url': '{{baseUrl}}/api/Housings/{{housingId}}',
        'scripts': [{'type': 'afterResponse', 'code': 'pm.test("Status is 200 or 204 (Deleted)", function () {\n    pm.expect(pm.response.code).to.be.oneOf([200, 204]);\n});'}]
    }
]

requests_data.sort(key=lambda r: r['order'])

items = []
for r in requests_data:
    item = {
        'name': r['name'],
        'event': make_event(r.get('scripts', [])),
        'request': {
            'method': r['method'],
            'header': [],
            'url': make_url(r['url'], r.get('query_params'))
        }
    }
    body = make_body(r.get('body'))
    if body:
        item['request']['body'] = body
    if not item['event']:
        del item['event']
    items.append(item)

collection = {
    'info': {
        'name': 'RoomMates_API_Tests',
        'schema': 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
    },
    'item': items,
    'variable': [
        {'key': 'baseUrl', 'value': '', 'type': 'string'},
        {'key': 'profileId', 'value': '', 'type': 'string'},
        {'key': 'housingId', 'value': '', 'type': 'string'}
    ]
}

with open('RoomMates_API_Tests.postman_collection.json', 'w', encoding='utf-8') as f:
    json.dump(collection, f, indent=2, ensure_ascii=False)

print('SUCCESS: written', len(items), 'items')
for i in items:
    print(' -', i['name'])

# FoodLoop — Tasks 3 of 3
## Release Candidate & Demo Readiness

هذه آخر مرحلة قبل التسليم والعرض. أحدث `develop` يحتوي على Stage 2 مدمجة ومختبرة؛ الهدف هنا هو **إخراج Release Candidate مستقرة وسهلة العرض**: إصلاح العيوب، تحسين UX، توحيد الواجهات، تقوية الاختبارات، وتحديث التوثيق. لا نضيف lifecycle جديد أو تغييرات كبيرة في الـDomain.

## البداية والتسليم للجميع

1. اسحبوا آخر `develop` قبل البدء:
   ```powershell
   git checkout develop
   git pull --ff-only origin develop
   ```
2. ابدأوا فقط من الفروع المذكورة أدناه. لا تكملوا على فروع Stage 1 أو Stage 2.
3. لا migrations أو enums أو schema changes في Stage 3 إلا بعد مراجعة براء والمالك المتأثر ووجود سبب ضروري.
4. خارج النطاق: LLM، SignalR، automatic expiry job، camera scanner، advanced reports، cancellation بعد courier assignment.
5. كل شاشة متغيرة يجب تجربتها على Desktop وعلى Mobile قريب من `390 × 844`.
6. إخفاء الزر ليس authorization؛ كل قاعدة صلاحيات وownership تظل enforced على السيرفر.
7. كل المواعيد الجديدة/المعدلة في الواجهة تعرض بوضوح على أنها UTC.
8. ممنوع وضع passwords أو secrets أو raw handover codes داخل Audit، logs، screenshots أو PR descriptions.
9. كل PR يجب أن يذكر:
   - Success scenario + result.
   - Rejected scenario + result.
   - Manual mobile check.
   - `dotnet build FoodLoop.slnx -c Release` result.
   - `dotnet test FoodLoop.slnx -c Release --verbosity minimal` result.
   - Screenshots للشاشات التي تغيرت.
10. براء ينسق تغييرات الملفات المشتركة مثل `_Layout.cshtml` والـglobal CSS وREADME لتقليل تعارضات الدمج.
11. لو blocker استمر أكثر من 30 دقيقة، اكتب سببه وما جربته قبل طلب المساعدة.
12. قبل طلب merge: pull آخر `develop`، حل التعارضات مع صاحب الـfeature لو كانت Business Logic، ثم أعد build/tests.

---

## Jana — Authentication and Organization UX

**Branch:** `feature/jana-stage3-auth-ux`

### المطلوب

1. دعم `ReturnUrl` في Login:
   - المستخدم المحول إلى Login يرجع للصفحة المطلوبة بعد نجاح الدخول.
   - يسمح فقط بـlocal URLs.
   - أي external URL يتم تجاهله ويعود المستخدم إلى Home/default safe destination.

2. تحسين رسائل حالة المؤسسة:
   - Pending: الحساب ينتظر موافقة الإدارة.
   - Rejected: طلب المؤسسة مرفوض.
   - Suspended Donor: الحساب موقوف.
   - Suspended Beneficiary: يسمح له بالدخول لقراءة My Claims فقط.

3. توحيد Login/Register باللغة الإنجليزية:
   - Labels واضحة.
   - Validation messages واضحة.
   - الاحتفاظ بالبيانات غير الحساسة عند فشل التسجيل.
   - لا تعيد عرض كلمة المرور في HTML.

4. تحسين صفحات إدارة المؤسسات على Mobile:
   - Status badges واضحة.
   - Approve/Reject/Suspend/Reactivate actions واضحة.
   - Confirmation قبل تغيير الحالة.
   - لا تغيير في transitions أو Identity roles الحالية.

5. اختبارات regression:
   - Safe local ReturnUrl.
   - رفض external ReturnUrl/open redirect.
   - Pending/Rejected/Suspended behavior.
   - Admin وCourier بدون Organization.
   - منع non-Admin من صفحات المؤسسات وdirect URLs.

### قبول التسليم

- Admin الذي يفتح صفحة محمية ثم يسجل الدخول يعود لنفس الصفحة.
- External ReturnUrl لا يعمل.
- كل Organization status تعرض رسالة صحيحة.
- لا password يعاد عرضه داخل HTML.
- الصفحات تعمل على Mobile بدون horizontal overflow.
- لا authorization rule تعتمد على UI فقط.

---

## Alaa — Donation Screens and Marketplace Polish

**Branch:** `feature/alaa-stage3-donations-ux`

### المطلوب

1. تحسين Create Donation وEdit Donation وMy Donations وMarketplace على Mobile.

2. توحيد عرض:
   - Status badges.
   - Quantity + unit.
   - Prepared/Expiry times مع UTC واضح.
   - Success/error messages.
   - Empty states.

3. الأزرار الظاهرة تطابق الحالة:
   - Draft: Edit وPublish.
   - Available/Claimed/delivery stages/Closed: لا Edit ولا Publish.
   - server-side validation يظل المرجع.

4. تحسين Marketplace:
   - search وcategory يظلان محفوظين مع Previous/Next.
   - رسالة واضحة عند عدم وجود نتائج.
   - لا تعرض Next عندما لا توجد صفحة لاحقة فعلًا.
   - الجداول/cards تعمل على Mobile أو داخل responsive container واضح.

5. مراجعة validation والownership:
   - Quantity > 0.
   - Expiry بعد PreparedAt.
   - Publish يتطلب expiry مستقبلية.
   - Donor آخر لا يستطيع Edit عبر direct URL.
   - Available marketplace لا يعرض expired أو suspended-donor donations.

### قبول التسليم

- Draft يمكن إنشاؤها، تعديلها، نشرها وتظهر النتيجة بعد Refresh.
- Filters تبقى محفوظة أثناء pagination.
- Donation غير المؤهلة لا تظهر في Marketplace.
- Foreign donor direct URL مرفوض.
- كل الوقت المعروض موسوم UTC.
- Mobile لا يفقد actions أو البيانات الأساسية.

---

## Safa — Claims History and Cancellation UX

**Branch:** `feature/safa-stage3-claims-ux`

### المطلوب

1. إنهاء responsive design لـMy Claims:
   - Status والخطوة الحالية واضحتان.
   - Cancelled وClosed يظلان في History.
   - لا فقد بيانات على Mobile.

2. Cancel UX:
   - يظهر فقط عندما `CanCancel = true`.
   - Confirmation قبل POST.
   - منع double submission من الواجهة.
   - السيرفر يظل مسؤولًا عن كل authorization/state checks.

3. Pagination/order:
   - لا Next إذا لا توجد صفحة أخرى.
   - ترتيب ثابت.
   - لا كشف لأي Claim تابعة لمؤسسة أخرى.

4. توحيد rejected states:
   - nonexistent أو foreign claim => NotFound بدون disclosure.
   - Suspended Beneficiary => read-only history ولا Cancel.
   - Assigned/PickupPending/InTransit/Closed => لا cancellation.

5. Regression tests:
   - Cancelled claim تبقى في History بعد Refresh.
   - Suspended Beneficiary read-only.
   - Cross-organization request.
   - Repeated Cancel لا ينشئ Audit جديد.
   - Cancel vs Assign concurrency: واحد فقط ينجح.

### قبول التسليم

- Claim قابلة للإلغاء تتحول Cancelled وتبقى في History.
- Donation ترجع للحالة الصحيحة حسب العقد الحالي.
- UI لا يعرض Cancel عندما لا يسمح، والـdirect POST يظل مرفوضًا.
- Pagination لا تنقل المستخدم إلى صفحة فارغة.
- لا duplicate audit أو stale-success في races.

---

## Haneen — Courier and Handover Demo UX

**Branch:** `feature/haneen-stage3-courier-ux`

### المطلوب

1. تحسين Assign Courier لعرض:
   - Donation title.
   - Donor + Beneficiary.
   - Pickup address.
   - Expiry UTC.
   - Current status.
   - Courier selection بشكل واضح.

2. تحسين My Tasks على Mobile:
   - المهمة الحالية.
   - Pickup address.
   - الحالة والخطوة التالية.
   - Closed تظهر Completed بلا Verification action.

3. تحسين Code issuance:
   - Pickup/Delivery واضح.
   - Actual server expiry UTC واضح.
   - QR والraw text يمثلان نفس token.
   - Copy button مع feedback وfallback.
   - لا localStorage ولا raw token في DB/Audit.

4. تحسين Verify Handover:
   - رسائل واضحة لـwrong/expired/reused code.
   - منع double submission من الواجهة.
   - Pickup الناجح => InTransit.
   - Delivery الناجح => Closed.

5. Regression tests:
   - Old token بعد regeneration مرفوض.
   - Expired/reused token مرفوض.
   - Wrong courier/wrong purpose مرفوض.
   - Delivery before Pickup مرفوض.
   - Closed task لا تتنفذ مرة ثانية.

### قبول التسليم

- رحلة Pickup ثم Delivery تعمل كاملة على Mobile.
- QR يفك إلى نفس raw token الموجود في صفحة الإصدار.
- raw token لا يظهر في صفحات لاحقة ولا Audit.
- حالات الرفض لا تغير DB ولا تنشئ Audit/Handover إضافيًا.
- Closed task تظهر بوضوح كـCompleted.

---

## Baraa — Final Integration, Shared UI and Release

**Branch:** `feature/baraa-stage3-release`

> يبدأ الدمج النهائي بعد جاهزية PRs الخاصة بباقي الفريق. لا يعيد كتابة Business Logic الخاصة بهم.

### المطلوب

1. توحيد shared layout:
   - Responsive navigation.
   - Role-specific navigation.
   - Active page indication إن أمكن بدون تعقيد.
   - Logout/alerts/page spacing بشكل موحد.
   - لا تعرض روابط عمليات غير مسموحة للدور، مع بقاء server authorization.

2. توحيد العناصر المشتركة:
   - Success/error alerts.
   - Empty states.
   - Status badges.
   - UTC date presentation.
   - AccessDenied/Error pages.
   - global CSS بعد دمج feature-specific UI لتقليل conflicts.

3. Security/integration regression pass:
   - direct URL authorization.
   - cross-organization access.
   - antiforgery للـunsafe MVC requests.
   - open-redirect protection في ReturnUrl.
   - no raw QR tokens in audit/localStorage.
   - no secrets في repo/screenshots.
   - server-side role/organization checks ما زالت تعمل.

4. تشغيل رحلة كاملة بقاعدة جديدة:
   - Register Donor + Beneficiary.
   - Admin approval.
   - Create/Edit/Publish Donation.
   - Marketplace search/filter + Claim.
   - Cancel claim مرة.
   - Claim جديد.
   - Assign Courier.
   - Pickup code + verification.
   - Delivery code + verification.
   - Closed dashboard count.
   - Audit filters.

5. التوثيق:
   - README.
   - SHARED-CONTRACTS.
   - TASKS-03.
   - Demo script.
   - Troubleshooting/setup checks.
   - ERD + physical database diagram ضمن deliverables النهائية/التقرير، مع التأكد أنها تصف schema الفعلية.
   - الوثائق يجب أن تصف الكود المنفذ فعلًا لا الـoriginal proposal.

6. Final verification:
   - Release build.
   - Full SQL-backed tests.
   - إنشاء **قاعدة جديدة** وتطبيق الـcommitted migrations الموجودة ثم seed؛ لا تنشئ migration جديدة إلا لو تمت الموافقة عليها.
   - Manual Desktop test.
   - Manual Mobile test (~390×844).
   - repo hygiene: لا secrets، duplicate project folders، MDF/generated DB files أو build artifacts.
   - تأكد أن GitHub Actions على develop خضراء بعد الدمج النهائي.

### قبول التسليم

- الرحلة كاملة تعمل من Registration إلى Closed.
- الأدوار الأربعة تعمل بصلاحياتها الصحيحة: Admin, Donor, Beneficiary, Courier.
- Release build بلا errors/warnings وفق baseline الحالي.
- جميع الاختبارات ناجحة.
- Demo قابل للتنفيذ في أقل من 8 دقائق.
- ERD/database diagram + README + shared contracts جاهزة للعرض.
- لا blocker معروف قبل العرض.

---

## ترتيب الدمج المقترح

1. Jana — Auth and organizations.
2. Alaa — Donations.
3. Safa — Claims.
4. Haneen — Courier and handover.
5. Baraa — Shared UI and final integration.

يمكن تجهيز PRs بالتوازي، لكن لا تبدأ تعديلات shared layout/global CSS النهائية قبل استقرار PRs الخاصة بالأعضاء. إذا حدث conflict في Claim/Courier business logic، يحله أصحاب الـfeatures مع براء؛ لا يتم اختيار نسخة ملف كاملة عشوائيًا.

## العرض النهائي لكل عضو

كل عضو يعرض في حوالي دقيقتين:

1. Success scenario.
2. Rejected scenario واحد.
3. Refresh يثبت أن النتيجة محفوظة.
4. اختبار يغطي السلوك.
5. Known limitation إن وجد.

## Final demo baseline

استخدموا قاعدة جديدة وحسابات demo/seed فقط. لا تستخدموا passwords أو بيانات حقيقية في العرض أو screenshots.

المسار المقترح:
Admin approval → Donor create/edit/publish → Beneficiary marketplace/claim → Admin assign courier → Pickup verification → Delivery verification → Closed → Audit review.

اختبروا cancellation كسيناريو Stage 2 مستقل قبل مسار التوصيل الكامل.

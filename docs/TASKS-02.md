# FoodLoop — Tasks 2 of 3

هذه خطة تنفيذ المرحلة الثانية، وليست وصفًا لميزات تم إنجازها بالفعل. baseline هو develop بعد دمج نسخة التكامل التي اختبرها براء. الهدف: استكمال التحكم في المؤسسات والتبرعات والحجوزات وتحسين الاستخدام. المرحلة الثالثة للتجربة النهائية، توحيد الواجهات، إصلاح العيوب، والتجهيز للعرض.

## البداية والتسليم للجميع

- بعد تأكيد براء الميرج: اسحبوا develop وابدأوا فرعًا جديدًا بالأسماء أدناه. لا تواصلوا على فروع المرحلة الأولى القديمة.
- لو عندك تغييرات غير محفوظة في Git، احفظيها في فرعك أولًا. لا تستخدمي reset --hard ولا تنسخي مجلد المشروع داخل نفسه.
- استخدمي قاعدة بيانات محلية وإعدادات README. Manage User Secrets على FoodLoop.Web فقط؛ تغيير Seed:DemoPassword لا يغير كلمات مرور المستخدمين الموجودين.
- أول 30 دقيقة: تأكيد تشغيل النسخة المدمجة ومراجعة الجزء الخاص بك. منتصف يوم العمل: ارفعي التقدم وافتحي Draft PR. قبل اجتماع الليلة بساعة: PR جاهز إلى develop مع الاختبارات. إن ظهر blocker أكثر من 30 دقيقة، اذكريه فورًا لبراء.
- التسليم: كود + شاشة تعمل + اختبار للنجاح وآخر للرفض على الأقل + وصف PR يوضح ماذا تغير وكيف جربتيه. عرض 3 دقائق في الاجتماع: نجاح، رفض، نتيجة محفوظة بعد Refresh.
- لا migrations ولا enums جديدة لهذه المهام. أي احتياج غير متوقع في schema يراجع مع براء والمالك المتأثر قبل التنفيذ.
- Application services تسجل عبر AddApplication، والـrepositories عبر AddInfrastructure. سجلي تغييرات الحالة وAudit في نفس SaveChanges؛ استخدمي RowVersion ولا تعيدي حفظ tracked entities بعد conflict.
- اللغة الأساسية للعناوين والأزرار الجديدة English؛ قواعد الصلاحيات على السيرفر. لا تعرضي بيانات مؤسسة ثانية أو كلمات مرور أو raw handover codes داخل الـAudit.

## Jana — إدارة المؤسسات | Organization management

Branch: feature/jana-stage2-organizations

**المطلوب:** صفحة Admin تعرض المؤسسات مع فلتر Status، وإضافة Suspend / Reactivate، مع استمرار PendingRequests الحالية.

1. تعرضي الاسم، النوع، الترخيص والحالة، بترتيب ثابت وpagination على السيرفر (20).
2. Admin فقط: Active -> Suspended وSuspended -> Active بأكشنز POST مع antiforgery ورسالة تأكيد. Pending/Rejected لا تدخل في الأكشنز الجديدة؛ تبقى Approve/Reject من Pending فقط.
3. OrganizationSuspended / OrganizationReactivated audit على Organization مع actor، في نفس حفظ الحالة. لا تغيري Identity Role، ولا تحذفي الحساب أو المؤسسة.
4. قبل التعليق: وضحي للأدمن أن التعليق يوقف العمليات الجديدة والتسليم للمطالبات الحالية؛ لا يلغيها تلقائيًا. إعادة التفعيل تسمح بالعمليات التي ما زالت شروطها الأخرى صحيحة (مثل expiry).
5. Suspended Beneficiary تدخل لقراءة My Claims الخاصة بجهتها فقط؛ Pending/Rejected ممنوعان. Suspended Donor لا يدخل. الجلسة المفتوحة قبل التعليق لا تسمح بتجاوز فحوص العمليات على السيرفر.

**قبول التسليم:** Suspend ثم Reactivate يغيران الحالة والسجل مرة واحدة، non-Admin مرفوض، تحديث متزامن يعطي conflict مفهومًا. اختبري جلسة Beneficiary مفتوحة قبل التعليق: Claim جديد مرفوض وMy Claims متاحة للقراءة. نسقي مع صفا دون تعديل ClaimService بنفسك.

## Alaa — تعديل المسودات والبحث | Draft editing and marketplace

Branch: feature/alaa-stage2-donations

**المطلوب:** Edit للتبرع Draft فقط، وبحث وفلتر category في Marketplace.

1. Active Donor يعدل Draft تابعًا لجهته فقط بنفس الحقول والـvalidation الحالية. لا تعديل لتبرع Available/Claimed أو مراحل التوصيل، ولا حذف في هذه المهمة.
2. Quantity > 0؛ expiry بعد prepared time. Publish يظل يشترط expiry مستقبلية. تغيير حالة التبرع أثناء فتح شاشة Edit يمنع حفظ نسخة قديمة مع رسالة واضحة.
3. DonationUpdated audit على FoodDonation في نفس الحفظ.
4. Marketplace: search على title + category اختيارية، مع تطبيق Available + unexpired + Active Donor دائمًا. pagination على السيرفر (20)، ترتيب ثابت، والاحتفاظ بالفلاتر مع Next/Previous؛ لا paging قبل الفلترة.
5. اكتبي timezone بوضوح بجوار المواعيد؛ التخزين يظل UTC. لا تغيري قواعد الـexpiry ولا تضيفي LLM/job.

**قبول التسليم:** تعديل Draft يظهر بعد Refresh؛ donor آخر مرفوض؛ Published لا يتعدل حتى من رابط مباشر؛ 21 تبرعًا صالحًا يثبتان pagination والفلاتر؛ المخفي بسبب expiry أو donor suspended لا يظهر بنتائج البحث. نسقي توقيع repository query مع براء قبل تعديله.

## Safa — إلغاء الحجز قبل إسناد المندوب | Cancel unassigned claim

Branch: feature/safa-stage2-cancel

**المطلوب:** زر Cancel في My Claims مع confirmation؛ إلغاء آمن للحجز قبل Assign فقط.

1. يسمح فقط لـActive Beneficiary بإلغاء Claim تابع لجهتها إذا كان Booked، بلا AssignedCourierUserId، والتبرع Claimed. تحقق السيرفر من الشروط في كل POST؛ لا تعتمد على إخفاء الزر.
2. Claim -> Cancelled. Donation -> Available إذا donor Active وexpiry مستقبلية، وإلا -> Draft. الاحتفاظ بالحجز الملغي في history؛ لا حذف لأي سجل.
3. ClaimCancelled event واحد على DonationClaim، Details تشمل DonationId وانتقال حالة التبرع؛ احفظي الاثنين والـAudit ذريًا.
4. PickupPending/InTransit/Closed لا تُلغى في هذه المرحلة. Suspended للقراءة فقط حتى لو عندها Claim قديم. لا code revocation مطلوب لأن الإلغاء قبل إسناد المندوب فقط.
5. تكرار الطلب لا ينشئ audit إضافيًا. سباق Cancel مع Assign لا يسمح بنجاح الاثنين؛ اعتمدي RowVersion للـclaim والتبرع وتعاملًا واضحًا مع PersistenceConflictException.

**قبول التسليم:** الإلغاء يعيد تبرعًا صالحًا للـMarketplace ويمكن حجزه من جديد؛ المنتهي أو donor suspended يعود Draft ولا يظهر؛ مؤسسة أخرى أو suspended لا تلغي؛ حجز مسند لا يلغى؛ اختبار SQL متزامن Cancel/Assign. اتفقي مع حنين على اختبار التكامل ولا تغيري lifecycle الخاص بالتوصيل.

## Haneen — تحسين تجربة أكواد التسليم | Handover code usability

Branch: feature/haneen-stage2-handover-ui

**المطلوب:** QR مرئي للكود الحالي + Copy code + عرض موعد انتهاء الكود، مع استمرار إدخال النص الحالي للمندوب.

1. صفحة إصدار الكود تعرض QR لنفس raw token، ونصًا يمكن نسخه وزر Copy يعطي نتيجة واضحة. لا تولدي token آخر من الواجهة.
2. اعرضي Pickup/Delivery ووقت expiry الفعلي القادم من السيرفر. لو أضفتِ countdown فهو إرشادي؛ السيرفر صاحب قرار قبول الكود.
3. اسم claim/donation الحالي يكفي للتعريف. استخدمي توليد QR داخل التطبيق/المتصفح؛ لا ترسلي الكود لخدمة QR خارجية. افحصي توافق أي مكتبة مع .NET 10 وسجليها في PR.
4. احتفظي بالـno-store وعرض الكود عند الإصدار فقط؛ لا localStorage ولا حفظ raw code في DB/Audit. regeneration الحالي يبطل الكود السابق، فلا تعرضي القديم كأنه صالح.
5. My Tasks: وضحي الحالة والخطوة المطلوبة وحالة Closed. لا تغيري انتقالات الحالة أو قواعد assignment/verification. camera scanner مؤجل، والنسخ/اللصق يظل مسارًا كاملًا صالحًا للعرض.

**قبول التسليم:** QR يفك لنفس النص، Copy يعمل مع fallback لتحديد النص، انتهاء الكود ظاهر، الكود القديم بعد regeneration مرفوض، الغلط/المستخدم/المندوب غير المسند مرفوضون، والـpickup -> delivery الحالي يظل يعمل. تغييرات DTO اللازمة للexpiry تخصك ونسقيها مع براء.

## Baraa — فلاتر Audit ومراجعة التكامل | Audit filters and integration

Branch: feature/baraa-stage2-audit

- Audit filters: action + actor + UTC date range [from, to)، مع validation أن from < to عند إدخالهما. pagination 20 وترتيب CreatedAtUtc ثم Id ثابت؛ total count بعد تطبيق الفلاتر.
- روابط dashboard تصل لصفحات Admin المناسبة، مع استمرار Admin authorization على السيرفر. لا SignalR أو charts/advanced reports.
- اختبر pagination بأكثر من 20 event؛ أحداث الإلغاء/التعليق الجديدة تظهر؛ non-Admin لا يقرأ بالروابط المباشرة.
- راجع العقود المشتركة مبكرًا ثم PR لكل عضو. لا تغييرات DB يدوية لإصلاح نتائج test. تأكد أن كل عضو يعمل على merged develop بقاعدة بياناته.
- راجع تسلسل الدمج بحسب الجاهزية والتبعيات؛ قبل اجتماع الليلة شغل build/tests ثم رحلة جديدة كاملة من التسجيل حتى Closed.

## حدود المرحلة الثالثة

بعد هذه المهام: إصلاح العيوب المتبقية، توحيد التصميم والمواعيد والرسائل، تجربة responsive، تدريب العرض وتوثيق الإعداد. لا نعد بإضافة LLM/SignalR/automatic expiry/camera scanner ضمن مهام 2 من 3.

## قالب التسليم

- Task / branch / PR:
- Implemented:
- Success scenario + result:
- Rejected scenario + result:
- Tests run + result:
- Shared contracts touched:
- Remaining blocker (or None):
